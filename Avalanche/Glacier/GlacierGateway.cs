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
        protected readonly string _vaultName;

        public GlacierGateway(GlacierParameters parameters)
        {
            _accessKeyId = parameters.AccessKeyId;
            _secretAccessKey = parameters.SecretAccessKey;
            _accountId = parameters.AccountId ?? "-";
            _region = parameters.GetRegion();
            _vaultName = parameters.VaultName;
        }

        protected IAmazonGlacier GetGlacierClient()
        {
            return new AmazonGlacierClient(_accessKeyId, _secretAccessKey, _region);
        }
        protected ArchiveTransferManager GetGlacierTransferManager()
        {
            return new ArchiveTransferManager(_accessKeyId, _secretAccessKey, _region);
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

        public Task<UploadResult> SaveImage(PictureModel picture, string vaultName = null, bool compress = true)
        {
            return SaveFile(Path.Combine(picture.AbsolutePath, picture.FileName), picture, string.IsNullOrEmpty(vaultName) ? _vaultName : vaultName, compress);
        }

        public Task<UploadResult> SaveFile(string filename, object metadata, string vaultName, bool compress)
        {
            var json = JsonConvert.SerializeObject(metadata);

            using (var client = GetGlacierTransferManager())
            {
                var fi = new FileInfo(filename);
                _log.InfoFormat("Uploading {0}, {1} bytes", filename, fi.Length);

                using (var percentUpdater = new ConsolePercentUpdater())
                {
                    percentUpdater.Start();
                    var vault = GetTrimmedVaultName(vaultName);
                    var options = new UploadOptions()
                    {
                        AccountId = _accountId,
                        StreamTransferProgress = (sender, args) =>
                        {
                            percentUpdater.PercentDone = args.PercentDone;
                        }
                    };
                    return client.UploadAsync(vault, json, filename, options);
                }                
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
            SevenZipBase.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
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
