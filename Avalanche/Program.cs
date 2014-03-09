using Amazon;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.Models;
using Avalanche.Repository;
using log4net;
using log4net.Config;
using Mono.Options;
using Newtonsoft.Json;
using SevenZip;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche
{
    public class Program
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        public static void Main(string[] args)
        {
            var parameters = ExecutionParameters.GetParametersFromArgs(args);
            if (parameters == null)
            {
                _log.Fatal("No config file could be found in the default location (my documents) and none was specified in the parameters.");
                Environment.Exit(0);
            }

            var errors = parameters.GetValidationErrors();
            if (errors.Any())
            {
                _log.Fatal("Configuration/parameter errors occurredt:");
                foreach (var e in errors)
                {
                    _log.Fatal(e);
                }
                Environment.Exit(0);
            }

            using (var insomniac = new Insomniac())
            {
                var lightroomRepo = new LightroomRepository(parameters.Avalanche.CatalongFilePath);
                var avalancheRepo = new AvalancheRepository(parameters.Avalanche.AvalancheFilePath);
                var gateway = new GlacierGateway(parameters.Glacier);

                var catalogId = lightroomRepo.GetUniqueId();
                var allPictures = lightroomRepo.GetAllPictures();
                var filteredPictures = allPictures.Where(a => a.LibraryCount > 0 && !avalancheRepo.FileIsArchived(a.FileId)).ToList();

                _log.InfoFormat("Backing up {0} images", filteredPictures.Count);

                var index = 0;
                foreach (var f in filteredPictures)
                {
                    _log.InfoFormat("Archiving {0}/{1}: {2}", ++index, filteredPictures.Count, Path.Combine(f.AbsolutePath, f.FileName));

                    // Try three times
                    ArchivedPictureModel archive = null;
                    for (var i = 0; i < 3; ++i)
                    {
                        try
                        {
                            archive = gateway.SaveImage(f, parameters.Glacier.VaultName);
                        }
                        catch (Exception ex)
                        {
                            _log.ErrorFormat("Error!!! {0}", ex);
                            continue;
                        }
                        break;
                    }

                    if (archive == null)
                    {
                        continue;
                    }

                    avalancheRepo.MarkFileAsArchived(archive, parameters.Glacier.VaultName, parameters.Glacier.Region, parameters.Avalanche.CatalongFilePath, catalogId.ToString());
                }

                _log.Info("Done");
                Console.Read();
            }
        }
    }
}