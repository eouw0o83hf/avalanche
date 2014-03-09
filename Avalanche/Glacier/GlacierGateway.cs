using Amazon;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using SevenZip;
using Avalanche.Models;
using Amazon.Glacier.Transfer;
using Amazon.Runtime;
using log4net;

namespace Avalanche.Glacier
{
    public class GlacierGateway
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        protected readonly string _accessKeyId;
        protected readonly string _secretAccessKey;
        protected readonly string _accountId;
        protected readonly RegionEndpoint _region;

        public GlacierGateway(GlacierParameters parameters)
        {
            _accessKeyId = parameters.AccessKeyId;
            _secretAccessKey = parameters.SecretAccessKey;
            _accountId = parameters.AccountId ?? "-";
            _region = parameters.GetRegion();
        }

        protected IAmazonGlacier GetGlacierClient()
        {
            return new AmazonGlacierClient(_accessKeyId, _secretAccessKey, _region);
        }

        #region Vaults

        public void AssertVaultExists(string vaultName)
        {
            vaultName = GetTrimmedVaultName(vaultName);
            var exists = VaultExists(vaultName);

            if (exists)
            {
                _log.DebugFormat("Vault {0} exists", vaultName);
                return;
            }

            _log.InfoFormat("Creating vault {0}", vaultName);
            using (var client = GetGlacierClient())
            {
                var result = client.CreateVault(new CreateVaultRequest
                {
                    AccountId = _accountId,
                    VaultName = vaultName
                });
                _log.DebugFormat("Vault creation result: {0}", result.HttpStatusCode);
            }
        }

        protected bool VaultExists(string vaultName)
        {
            vaultName = GetTrimmedVaultName(vaultName);

            using (var client = GetGlacierClient())
            {
                var response = client.ListVaults(new ListVaultsRequest
                {
                    AccountId = _accountId
                });

                return response.VaultList.Any(a => a.VaultName.Equals(vaultName));
            }
        }

        protected string GetTrimmedVaultName(string vaultName)
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

        public ArchivedPictureModel SaveImage(PictureModel picture, string vaultName = "Pictures", bool compress = true)
        {
            var archive = SaveFile(Path.Combine(picture.AbsolutePath, picture.FileName), picture, vaultName, compress);
            return new ArchivedPictureModel
            {
                Archive = archive,
                Picture = picture
            };
        }

        public ArchiveModel SaveFile(string filename, object metadata, string vaultName, bool compress)
        {
            var json = JsonConvert.SerializeObject(metadata);

            using (var fileStream = GetFileStream(filename, compress, json))
            using (var client = GetGlacierClient())
            {
                _log.InfoFormat("Uploading {0}, {1} bytes", filename, fileStream.Length);

                var hash = TreeHashGenerator.CalculateTreeHash(fileStream);
                fileStream.Position = 0;

                UploadArchiveResponse result;
                using (var percentUpdater = new ConsolePercentUpdater())
                {
                    percentUpdater.Start();

                    result = client.UploadArchive(new UploadArchiveRequest
                    {
                        AccountId = _accountId,
                        ArchiveDescription = json,
                        VaultName = GetTrimmedVaultName(vaultName),
                        Body = fileStream,
                        Checksum = hash,
                        StreamTransferProgress = new EventHandler<StreamTransferProgressArgs>((a, b) =>
                            {
                                percentUpdater.PercentDone = b.PercentDone;
                            })
                    });
                }

                _log.InfoFormat("File uploaded: {0}, archive ID: {1}", result.HttpStatusCode, result.ArchiveId);

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

        protected Stream GetFileStream(string filename, bool compress, string metadata, string metadataFilename = "metadata.txt")
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
            
            // Setup the compressor
            SevenZipCompressor.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
            var compressor = new SevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionMode = CompressionMode.Create,
                CompressionLevel = CompressionLevel.Ultra
            };

            // Compress!
            var compressedStream = new MemoryStream();
            compressor.CompressStreamDictionary(compressions, compressedStream);
            compressedStream.Position = 0;

            _log.InfoFormat("Compressed {0} from {1} to {2}, removing {3:0.00}%", filenameOnly, inputFileLength, compressedStream.Length, (float)((inputFileLength - compressedStream.Length) * 100) / inputFileLength);
            
            return compressedStream;
        }

        #endregion

        #region Load

        public void BeginVaultInventoryRetrieval(string vaultName, string notificationTargetTopicId)
        {
            vaultName = GetTrimmedVaultName(vaultName);
            using (var client = GetGlacierClient())
            {
                var response = client.InitiateJob(new InitiateJobRequest
                {
                    VaultName = vaultName,
                    JobParameters = new JobParameters
                    {
                        Type = "inventory-retrieval",
                        SNSTopic = notificationTargetTopicId
                    }
                });
                _log.DebugFormat("Job ID: {0}", response.JobId);
            }
        }

        public void PickupVaultInventoryRetrieval(string vaultName, string jobId, string outputFileName)
        {
            using(var client = GetGlacierClient())
            {
                var result = client.GetJobOutput(new GetJobOutputRequest
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
