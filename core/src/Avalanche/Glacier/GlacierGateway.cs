using Amazon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Avalanche.Models;
using Amazon.Runtime;
using Microsoft.Framework.Logging;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using System.IO.Compression;

namespace Avalanche.Glacier
{
    public class GlacierGateway
    {
        private readonly ILogger<GlacierGateway> _logger;
        private readonly IConsolePercentUpdater _updater;
        
        private readonly string _accessKeyId;
        private readonly string _secretAccessKey;
        private readonly string _accountId;
        private readonly RegionEndpoint _region;

        public GlacierGateway(GlacierParameters parameters, ILogger<GlacierGateway> logger,
                                IConsolePercentUpdater updater)
        {
            _accessKeyId = parameters.AccessKeyId;
            _secretAccessKey = parameters.SecretAccessKey;
            _accountId = parameters.AccountId ?? "-";
            _region = parameters.GetRegion();

            _logger = logger;
            _updater = updater;
        }

        private IAmazonGlacier GetGlacierClient()
        {
            return new AmazonGlacierClient(_accessKeyId, _secretAccessKey, _region);
        }

        #region Vaults

        public async Task AssertVaultExists(string vaultName)
        {
            vaultName = GetTrimmedVaultName(vaultName);
            var exists = await VaultExists(vaultName);

            if (exists)
            {
                _logger.LogDebug("Vault {0} exists", vaultName);
                return;
            }

            _logger.LogInformation("Creating vault {0}", vaultName);
            using (var client = GetGlacierClient())
            {
                var result = await client.CreateVaultAsync(new CreateVaultRequest
                {
                    AccountId = _accountId,
                    VaultName = vaultName
                });
                _logger.LogDebug("Vault creation result: {0}", result.HttpStatusCode);
            }
        }

        private async Task<bool> VaultExists(string vaultName)
        {
            vaultName = GetTrimmedVaultName(vaultName);

            using (var client = GetGlacierClient())
            {
                var response = await client.ListVaultsAsync(new ListVaultsRequest
                {
                    AccountId = _accountId
                });

                return response.VaultList.Any(a => a.VaultName.Equals(vaultName));
            }
        }

        private string GetTrimmedVaultName(string vaultName)
        {
            if (string.IsNullOrWhiteSpace(vaultName))
            {
                throw new ArgumentException("Value cannot be null/empty", "vaultName");
            }

            vaultName = vaultName.Trim();
            return vaultName;
        }

        #endregion

        #region Save

        public async Task<ArchivedPictureModel> SaveImage(PictureModel picture, string vaultName = "Pictures", bool compress = true)
        {
            var archive = await SaveFile(Path.Combine(picture.AbsolutePath, picture.FileName), picture, vaultName, compress);
            return new ArchivedPictureModel
            {
                Archive = archive,
                Picture = picture
            };
        }

        public async Task<ArchiveModel> SaveFile(string filename, object metadata, string vaultName, bool compress)
        {
            var json = JsonConvert.SerializeObject(metadata);

            using (var fileStream = await GetFileStream(filename, compress, json))
            using (var client = GetGlacierClient())
            {
                _logger.LogInformation("Uploading {0}, {1} bytes", filename, fileStream.Length);

                var hash = TreeHashGenerator.CalculateTreeHash(fileStream);
                fileStream.Position = 0;

                _updater.UpdatePercentage(filename, 0);
                var result = await client.UploadArchiveAsync(new UploadArchiveRequest
                {
                    AccountId = _accountId,
                    ArchiveDescription = json,
                    VaultName = GetTrimmedVaultName(vaultName),
                    Body = fileStream,
                    Checksum = hash,
                    StreamTransferProgress = new EventHandler<StreamTransferProgressArgs>((a, b) =>
                        {
                            _updater.UpdatePercentage(filename, b.PercentDone);
                        })
                });

                _logger.LogInformation("File uploaded: {0}, archive ID: {1}", result.HttpStatusCode, result.ArchiveId);

                var response = new ArchiveModel
                {
                    ArchiveId = result.ArchiveId,
                    Status = result.HttpStatusCode,
                    Location = result.Location,
                    Metadata = JsonConvert.SerializeObject(result.ResponseMetadata),
                    PostedTimestamp = DateTime.UtcNow
                };

                return response;
            }
        }

        private async Task<Stream> GetFileStream(string filename, bool compress, string metadata, string metadataFilename = "metadata.txt")
        {
            var file = File.OpenRead(filename);
            if (!compress)
            {
                return file;
            }

            var inputFileLength = file.Length;

            // Setup the streams to be zipped 
            var compressions = new Dictionary<string, Stream>();

            var filenameOnly = Path.GetFileName(filename);
            compressions[filenameOnly] = file;

            var metadataStream = new MemoryStream(Encoding.Unicode.GetBytes(metadata));
            // Make sure there's not some bizarre filename coincidence
            if (metadataFilename == filenameOnly)
            {
                metadataFilename += ".actuallythemetadata.txt";
            }
            compressions[metadataFilename] = metadataStream;
            
            // Compress!
            var compressedStream = new MemoryStream();

            // Might need to change `true` to `false`
            using(var archive = new ZipArchive(compressedStream, ZipArchiveMode.Create, true))
            {
                foreach(var item in compressions)
                {
                    var currentEntry = archive.CreateEntry(item.Key, CompressionLevel.Optimal);
                    using(var entryStream = currentEntry.Open())
                    {
                        await item.Value.CopyToAsync(entryStream);
                    }
                }
            }
            
            compressedStream.Position = 0;

            _logger.LogInformation("Compressed {0} from {1} to {2}, removing {3:0.00}%", filenameOnly, inputFileLength, compressedStream.Length, (float)((inputFileLength - compressedStream.Length) * 100) / inputFileLength);
            
            return compressedStream;
        }

        #endregion

        #region Load

        public async Task BeginVaultInventoryRetrieval(string vaultName, string notificationTargetTopicId)
        {
            vaultName = GetTrimmedVaultName(vaultName);
            using (var client = GetGlacierClient())
            {
                var response = await client.InitiateJobAsync(new InitiateJobRequest
                {
                    VaultName = vaultName,
                    JobParameters = new JobParameters
                    {
                        Type = "inventory-retrieval",
                        SNSTopic = notificationTargetTopicId
                    }
                });
                _logger.LogDebug("Job ID: {0}", response.JobId);
            }
        }

        public async Task PickupVaultInventoryRetrieval(string vaultName, string jobId, string outputFileName)
        {
            using(var client = GetGlacierClient())
            {
                var result = await client.GetJobOutputAsync(new GetJobOutputRequest
                {
                    AccountId = _accountId,
                    JobId = jobId,
                    VaultName = vaultName
                });

                using (var file = File.OpenWrite(outputFileName))
                {
                    result.Body.CopyTo(file);
                }
            }
        }

        #endregion
    }
}
