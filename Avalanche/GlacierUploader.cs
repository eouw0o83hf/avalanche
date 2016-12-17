using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public GlacierUploader(LightroomRepository lightroomRepository, GlacierGateway glacierGateway)
        {
            _lightroomRepository = lightroomRepository;
            _glacierGateway = glacierGateway;
        }

        public void RunUploader(ICollection<PictureModel> allPictures)
        {
            var catalogId = _lightroomRepository.GetUniqueId();
            var filteredPictures =
                allPictures.Where(a => a.LibraryCount > 0 && string.IsNullOrWhiteSpace(a.CopyName)).ToList();

            Log.InfoFormat("Backing up {0} images", filteredPictures.Count);

            Task.Factory.StartNew(RunUploadCompleteQueue);

            var tasks = new List<Task>();
            var index = 0;
            foreach (var f in filteredPictures)
            {
                Log.InfoFormat("Archiving {0}/{1}: {2}", ++index, filteredPictures.Count, Path.Combine(f.AbsolutePath, f.FileName));

                try
                {
                    tasks.Add(_glacierGateway
                        .SaveImage(f)
                        .ContinueWith(task => UploadQueue.Add(new UploadBag
                        {
                            PictureModel = f,
                            Result = task.Result
                        }))
                    );
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error!!! {0}", ex);
                    continue;
                }

                if (tasks.Count > 5)
                {
                    Task.WhenAny(tasks).ContinueWith(t =>
                    {
                        tasks.Remove(t);
                    });
                }
            }
            Task.WhenAll(tasks).Wait();
            Log.InfoFormat("All upload tasks have completed...");
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
                Log.InfoFormat("Upload completed: {0}", bag.PictureModel.FileName);

                _lightroomRepository.MarkAsArchived(archive, bag.PictureModel);
            }
        }
    }
}
