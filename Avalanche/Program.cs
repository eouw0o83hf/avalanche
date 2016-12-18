using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.Models;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Glacier.Transfer;
using Newtonsoft.Json;

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

            var lightroomRepository = new LightroomRepository(parameters.Avalanche.CatalongFilePath);
            var glacierGateway = new GlacierGateway(parameters.Glacier);
            var allPictures = lightroomRepository.GetAllPictures();
            var toUpload = allPictures.Where(a => a.LibraryCount > 0 && string.IsNullOrWhiteSpace(a.CopyName)).ToList();

            using (var insomniac = new Insomniac())
            {
                var uploader = new GlacierUploader(lightroomRepository, glacierGateway);
                uploader.RunUploader(toUpload);
            }

            var missing = allPictures.Where(p => !File.Exists(Path.Combine(p.AbsolutePath, p.FileName))).ToList();
            Log.InfoFormat("Downloading {0} missing files...", missing.Count);

            Log.Info("Done");
        }        
        
    }
}