using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.Glacier.Transfer;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.Models;
using log4net;

namespace Avalanche
{
    public class GlacierUploader
    {
        private class UploadBag
        {
            public UploadResult Result { get; set; }
            public PictureModel PictureModel { get; set; }
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));
        private static readonly BlockingCollection<UploadBag> UploadQueue = new BlockingCollection<UploadBag>();
        private readonly LightroomRepository _lightroomRepository;
        private readonly GlacierGateway _glacierGateway;
        private readonly bool _dryRun;

        public GlacierUploader(LightroomRepository lightroomRepository, GlacierGateway glacierGateway, bool dryRun = false)
        {
            _lightroomRepository = lightroomRepository;
            _glacierGateway = glacierGateway;
            _dryRun = dryRun;
        }

        public void RunUploader(ICollection<PictureModel> pictures)
        {
            Log.InfoFormat("Backing up {0} images", pictures.Count);

            Task.Factory
                .StartNew(RunUploadCompleteQueue)
                .ContinueWith(t => {
                    Log.InfoFormat("Upload complete queue finished. Exception: {0}", t.Exception);
                });

            var tasks = new List<Task>();
            var index = 0;
            foreach (var f in pictures)
            {
                Log.InfoFormat("Archiving {0}/{1}: {2}", ++index, pictures.Count, Path.Combine(f.AbsolutePath, f.FileName));

                var t = BeginArchive(f);
                if (t == null)
                {
                    continue;
                }

                lock (tasks)
                {
                    Log.DebugFormat("Adding task, id: {0}", t.Id);
                    tasks.Add(t);

                    // purge completed tasks
                    tasks.RemoveAll(task => task.IsCompleted);
                    Log.DebugFormat("After purge, count: {0}", tasks.Count);

                    // keep adding tasks until capacity
                    if (tasks.Count <= 5)
                    {
                        continue;
                    }
                }

                Log.DebugFormat("Waiting for any task to complete, count: {0}", tasks.Count);
                Task.WhenAny(tasks).Wait();                   
            }
            Task.WhenAll(tasks).Wait();
            Log.InfoFormat("All upload tasks have completed...");
        }

        private Task BeginArchive(PictureModel pictureModel)
        {
            if (_dryRun)
            {                
                return Task.Factory.StartNew(() =>
                {
                    Log.InfoFormat("Dry run: {0}", pictureModel.FileName);
                });
            }

            try
            {
                var saveTask = _glacierGateway
                    .SaveImageAsync(pictureModel)
                    .ContinueWith(task =>
                    {
                        UploadQueue.Add(new UploadBag
                        {
                            PictureModel = pictureModel,
                            Result = task.Result
                        });
                    });

                return saveTask;                
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Error!!! {0}", ex);               
            }
            return null;
        }

        private void RunUploadCompleteQueue()
        {
            while (!UploadQueue.IsCompleted)
            {
                var bag = UploadQueue.Take();

                var result = bag.Result;
                var archive = new ArchiveModel
                {
                    ArchiveId = result.ArchiveId,
                    PostedTimestamp = DateTime.UtcNow
                };
                Log.InfoFormat("Upload completed: {0}, {1}", bag.PictureModel.FileName, bag.Result.ArchiveId);

                if (_lightroomRepository.MarkAsArchived(archive, bag.PictureModel) < 1)
                {
                    Log.ErrorFormat("Failed to mark image as archived: {0}, archive Id: {1}", bag.PictureModel.FileName, bag.Result.ArchiveId);
                }
            }
        }
    }
}
