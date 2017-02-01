using Avalanche.Glacier;
using Avalanche.Lightroom;
using log4net;
using System;
using System.IO;
using System.Linq;

namespace Avalanche
{
    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        public static void Main(string[] args)
        {
            var parameters = ExecutionParameters.GetParametersFromArgs(args);
            if (parameters == null)
            {
                Log.Fatal("No config file could be found in the default location (my documents) and none was specified in the parameters.");
                Environment.Exit(0);
            }

            var errors = parameters.GetValidationErrors();
            if (errors.Any())
            {
                Log.Fatal("Configuration/parameter errors occurredt:");
                foreach (var e in errors)
                {
                    Log.Fatal(e);
                }
                Environment.Exit(0);
            }

            Log.InfoFormat("Glacier valut: {0}", parameters.Glacier.VaultName);
            var glacierGateway = new GlacierGateway(parameters.Glacier);

            Log.InfoFormat("Archiving pictures from: {0}", parameters.Avalanche.CatalongFilePath);
            var lightroomRepository = new LightroomRepository(parameters.Avalanche.CatalongFilePath);

            Log.InfoFormat("Collection name: {0}", parameters.Avalanche.AdobeCollectionName);
            var allPictures = lightroomRepository.GetAllPictures(parameters.Avalanche.AdobeCollectionName);
            var toUpload = allPictures.Where(a => a.LibraryCount > 0 && string.IsNullOrWhiteSpace(a.CopyName)).ToList();

            Log.InfoFormat("Selected {0} pictures out from {1}", toUpload.Count, allPictures.Count);
            using (var insomniac = new Insomniac())
            {
                var uploader = new GlacierUploader(lightroomRepository, glacierGateway, parameters.DryRun);
                uploader.RunUploader(toUpload);
            }

            var missing = allPictures.Where(p => !File.Exists(Path.Combine(p.AbsolutePath, p.FileName))).ToList();
            Log.InfoFormat("Downloading {0} missing files...", missing.Count);

            Log.Info("Done");
        }        
        
    }
}