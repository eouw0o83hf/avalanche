using System;
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
        
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly IGlacierGateway _glacier;
        private readonly ILightroomReader _lightroom;
        private readonly IAvalancheRepository _avalanche;
        private readonly ExecutionParameters _parameters;
        
        public AvalancheRunner(ILogger<AvalancheRunner> logger, IGlacierGateway glacier,
                               ILightroomReader lightroom, IAvalancheRepository avalanche,
                               ExecutionParameters parameters)
        {
            _logger = logger;
            _glacier = glacier;
            _lightroom = lightroom;
            _avalanche = avalanche;
            _parameters = parameters;
        }

        public async Task<AvalancheRunResult> Run()
        {
            var result = new AvalancheRunResult();
            
            var catalogId = _lightroom.GetCatalogId();
            var allPictures = _lightroom.GetAllPictures();
            var filteredPictures = allPictures
                                    .GroupBy(a => a.FileId)
                                    .Select(a => a.First())
                                    .Where(a => a.LibraryCount > 0 
                                                && !_avalanche.FileIsArchived(a.FileId))
                                    .ToList();

            _logger.LogInformation("Backing up {0} images", filteredPictures.Count);
            
            var index = 0;
            foreach(var f in filteredPictures)
            {
                var currentPath = Path.Combine(f.AbsolutePath, f.FileName);
                _logger.LogInformation("Archiving {0} of {1}: {2}", ++index, filteredPictures.Count, currentPath);

                // Retry for transient transport failures
                ArchivedPictureModel archive = null;
                for(var i = 0; i < RetryCount; ++i)
                {
                    try
                    {
                        archive = await _glacier.SaveImage(f, _parameters.Glacier.VaultName);
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
                    result.Failures.Add(f);
                    continue;
                }

                _avalanche.MarkFileAsArchived(archive, _parameters.Glacier.VaultName, _parameters.Glacier.Region, _parameters.Avalanche.CatalogFilePath, catalogId.ToString());
                result.Successes.Add(f);
            }

            _logger.LogInformation("Done");
            return result;
        }
    }
}