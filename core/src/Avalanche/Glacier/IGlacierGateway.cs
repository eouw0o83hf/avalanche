using Amazon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Avalanche.Models;
using Amazon.Runtime;
using Microsoft.Framework.Logging;
using Amazon.Glacier;
using Amazon.Glacier.Model;

namespace Avalanche.Glacier
{
    /// <summary>
    /// Wraps the AWS Glacier API using Avalanche models and context so it's easy to call
    /// </summary>
    public interface IGlacierGateway
    {
        Task AssertVaultExists(string vaultName);
        Task<ArchivedPictureModel> SaveImage(PictureModel picture, string vaultName = "Pictures");
        
        Task BeginVaultInventoryRetrieval(string vaultName, string notificationTargetTopicId);
        Task PickupVaultInventoryRetrieval(string vaultName, string jobId, string outputFileName);
    }
    
    public class GlacierGateway : IGlacierGateway
    {
        private readonly IAmazonGlacier _glacier;
        private readonly ILogger<GlacierGateway> _logger;
        private readonly IConsolePercentUpdater _updater;
        private readonly IArchiveProvider _archiveProvider;
        
        private readonly string _accountId;
        private readonly bool _testMode;

        public GlacierGateway(IAmazonGlacier glacier, ILogger<GlacierGateway> logger,
                              IConsolePercentUpdater updater, IArchiveProvider archiveProvider,
                              string accountId, bool testMode)
        {
            _glacier = glacier;
            _logger = logger;
            _updater = updater;
            _archiveProvider = archiveProvider;
            _accountId = accountId;
            _testMode = testMode;
        }

        public async Task AssertVaultExists(string vaultName)
        {
            vaultName = GetTrimmedVaultName(vaultName);

            var listResponse = await _glacier.ListVaultsAsync(new ListVaultsRequest
            {
                AccountId = _accountId
            });
            if(listResponse.VaultList.Any(a => a.VaultName.Equals(vaultName)))
            {
                return;
            }

            if(_testMode)
            {
                _logger.LogInformation("Would create vault {0}, but we're in test mode so I'm not", vaultName);
            }
            else
            {
                _logger.LogInformation("Creating vault {0}", vaultName);
                var result = await _glacier.CreateVaultAsync(new CreateVaultRequest
                {
                    AccountId = _accountId,
                    VaultName = vaultName
                });
                _logger.LogDebug("Vault creation result: {0}", result.HttpStatusCode);
            }
        }

        public async Task<ArchivedPictureModel> SaveImage(PictureModel picture, string vaultName = "Pictures")
        {
            var archive = await SaveFileWithMetadata(Path.Combine(picture.AbsolutePath, picture.FileName), picture, vaultName);
            return new ArchivedPictureModel
            {
                Archive = archive,
                Picture = picture
            };
        }

        private async Task<ArchiveModel> SaveFileWithMetadata(string filename, object metadata, string vaultName)
        {
            var json = JsonConvert.SerializeObject(metadata);

            using (var fileStream = await _archiveProvider.GetFileStream(filename, json))
            {
                var hash = TreeHashGenerator.CalculateTreeHash(fileStream);
                fileStream.Position = 0;

                _updater.UpdatePercentage(filename, 0);
                var result = await DoGlacierUpload(json, vaultName, fileStream, hash, filename);

                return new ArchiveModel
                {
                    ArchiveId = result.ArchiveId,
                    Status = result.HttpStatusCode,
                    Location = result.Location,
                    Metadata = JsonConvert.SerializeObject(result.ResponseMetadata),
                    PostedTimestamp = DateTime.UtcNow
                };
            }
        }

        // This is pulled into its own method to make the test-mode-bypass logic
        // simpler and very straightforward
        private async Task<UploadArchiveResponse> DoGlacierUpload(string metadataJson, string vaultName, Stream fileStream, string hash, string filename)
        {
            if(_testMode)
            {
                _logger.LogInformation("Would upload {0} to AWS, but we're in test mode so I'm not", filename);
                return new UploadArchiveResponse();
            }
            
            _logger.LogInformation("Uploading {0} to AWS, {1} bytes", filename, fileStream.Length);

            var result = await _glacier.UploadArchiveAsync(new UploadArchiveRequest
                {
                    AccountId = _accountId,
                    ArchiveDescription = metadataJson,
                    VaultName = GetTrimmedVaultName(vaultName),
                    Body = fileStream,
                    Checksum = hash,
                    StreamTransferProgress = new EventHandler<StreamTransferProgressArgs>((a, b) =>
                        {
                            _updater.UpdatePercentage(filename, b.PercentDone);
                        })
                });

            _logger.LogInformation("File uploaded: {0}, archive ID: {1}", result.HttpStatusCode, result.ArchiveId);

            return result;
        }

        public async Task BeginVaultInventoryRetrieval(string vaultName, string notificationTargetTopicId)
        {
            vaultName = GetTrimmedVaultName(vaultName);
            var response = await _glacier.InitiateJobAsync(new InitiateJobRequest
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

        public async Task PickupVaultInventoryRetrieval(string vaultName, string jobId, string outputFileName)
        {
            var result = await _glacier.GetJobOutputAsync(new GetJobOutputRequest
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

        private static string GetTrimmedVaultName(string vaultName)
        {
            if (string.IsNullOrWhiteSpace(vaultName))
            {
                throw new ArgumentException("Value cannot be null/empty", "vaultName");
            }

            vaultName = vaultName.Trim();
            return vaultName;
        }
    }
}
