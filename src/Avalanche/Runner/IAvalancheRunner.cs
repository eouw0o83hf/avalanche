using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.Models;
using Avalanche.State;
using Microsoft.Framework.Logging;

namespace Avalanche.Runner
{
    public interface IAvalancheRunner
    {
        Task<AvalancheRunResult> Run();
    }
    
    public class AvalancheRunner : IAvalancheRunner
    {
        private const int RetryCount = 3;
        private const int UploadParallelism = 3;
        
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly IInjectionFactory<IGlacierGateway> _glacierFactory;
        private readonly IInjectionFactory<ILightroomReader> _lightroomFactory;
        private readonly IInjectionFactory<IAvalancheRepository> _avalancheFactory;
        private readonly ExecutionParameters _parameters;
        
        private class RunState
        {
            public ConcurrentQueue<PictureModel> WorkQueue { get; set; }
            
            public int Index { get; set; }
            public int FilteredPictureCount { get; set; }

            public Guid CatalogId { get; set; }

            public AvalancheRunResult Result { get; set; }
        }

        public AvalancheRunner(ILogger<AvalancheRunner> logger, 
                               IInjectionFactory<IGlacierGateway> glacierFactory,
                               IInjectionFactory<ILightroomReader> lightroomFactory, 
                               IInjectionFactory<IAvalancheRepository> avalancheFactory,
                               ExecutionParameters parameters)
        {
            _logger = logger;
            _glacierFactory = glacierFactory;
            _lightroomFactory = lightroomFactory;
            _avalancheFactory = avalancheFactory;
            _parameters = parameters;
        }

        public async Task<AvalancheRunResult> Run()
        {
            // This needs to be created or else all of the upload attempts will fail.
            // It's the container that the archives live in.
            using(var glacier = _glacierFactory.CreateWrapper())
            {                     
                await glacier.Item.AssertVaultExists(_parameters.Glacier.VaultName);
            }

            // In order to support upload parallelism, we're going to place
            // all of the work to be done in a Queue, then launch workers
            // which will pull from it. There's probably a more sophisticated
            // way to do this with stringing Tasks together, but the complexity
            // and number of edge cases are pretty intense. Having some longer-
            // running Tasks is way more foolproof.
            ConcurrentQueue<PictureModel> workQueue;
            Guid catalogId;

            using(var lightroom = _lightroomFactory.CreateWrapper())
            using(var avalanche = _avalancheFactory.CreateWrapper())
            {
                var filteredPictures = lightroom.Item.GetAllPictures()
                                        // A Picture can be in multiple Catalogs, but
                                        // only needs to be backed up a single time
                                        .GroupBy(a => a.FileId)
                                        .Select(a => a.First())
                                        .Where(a => a.LibraryCount > 0 
                                                    // Make sure we don't double archive
                                                    && !avalanche.Item.FileIsArchived(a.FileId));
                workQueue = new ConcurrentQueue<PictureModel>(filteredPictures);

                catalogId = lightroom.Item.GetCatalogId();
            }

            
            _logger.LogInformation("Backing up {0} images", workQueue.Count);

            var runState = new RunState
            {
                Index = 0,
                FilteredPictureCount = workQueue.Count,
                CatalogId = catalogId,
                Result = new AvalancheRunResult(),
                WorkQueue = workQueue
            };

            // Start up some long-running Tasks which will pull the
            // archive work from the Queue
            var workers = Enumerable.Range(0, UploadParallelism)
                                    .Select(a => ArchiveWorker(runState));
            await Task.WhenAll(workers);

            _logger.LogInformation("Done");
            return runState.Result;
        }

        private async Task ArchiveWorker(RunState state)
        {
            // If this were Scala we could just make this a tail-recursive method.
            // But it's not. So here's that one time where it would be nice.
            while(state.WorkQueue.Any())
            {
                PictureModel picture;
                if(!state.WorkQueue.TryDequeue(out picture))
                {
                    return;
                }

                await ArchivePicture(picture, state);
            }
        }

        private async Task ArchivePicture(PictureModel picture, RunState state)
        {
            var currentPath = Path.Combine(picture.AbsolutePath, picture.FileName);
            _logger.LogInformation("Archiving {0} of {1}: {2}", ++state.Index, state.FilteredPictureCount, currentPath);

            // Retry for transient transport failures
            ArchivedPictureModel archive = null;
            for(var i = 0; i < RetryCount; ++i)
            {
                try
                {
                    using(var glacier = _glacierFactory.CreateWrapper())
                    {
                        archive = await glacier.Item.SaveImage(picture, _parameters.Glacier.VaultName);
                    }
                    break;
                }
                catch(Exception ex)
                {
                    _logger.LogError("Error persisting file", ex);
                    continue;
                }
            }

            if(archive == null)
            {
                _logger.LogError("Failed 3 times to persist {0}, giving up", currentPath);
                state.Result.Failures.Add(picture);
                return;
            }

            using(var avalanche = _avalancheFactory.CreateWrapper())
            {
                avalanche.Item.MarkFileAsArchived(archive, _parameters.Glacier.VaultName, 
                                                _parameters.Glacier.Region, _parameters.Avalanche.CatalogFilePath, 
                                                state.CatalogId.ToString());
            }
            state.Result.Successes.Add(picture);
        }

    }
}