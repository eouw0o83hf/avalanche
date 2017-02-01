using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Framework.Logging;
using System.IO.Compression;

namespace Avalanche.Glacier
{
    public interface IArchiveProvider
    {
        /// <summary>
        /// Returns a filestream of a compressed archive containing the file referenced by @filename
        /// and a text file containing the contents of @metadata
        /// </summary>
        Task<Stream> GetFileStream(string filename, string metadata);
    }

    public class ArchiveProvider : IArchiveProvider
    {
        private const string MetadataFilename = "metadata.txt";

        private readonly ILogger<ArchiveProvider> _logger;

        public ArchiveProvider(ILogger<ArchiveProvider> logger)
        {
            _logger = logger;
        }

        public async Task<Stream> GetFileStream(string filename, string metadata)
        {
            var outputStream = new MemoryStream();
            long inputFileLength;
            var filenameOnly = Path.GetFileName(filename);

            // Might need to change `true` to `false`
            using(var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {

                var fileEntry = archive.CreateEntry(filenameOnly, CompressionLevel.Optimal);
                using(var fileStream = File.OpenRead(filename))
                using(var entryStream = fileEntry.Open())
                {
                    inputFileLength = fileStream.Length;
                    await fileStream.CopyToAsync(entryStream);
                }

                var metadataFilename = MetadataFilename;
                if(metadataFilename == filenameOnly)
                {
                    metadataFilename += ".actuallythemetadata.txt";
                }

                var metadataEntry = archive.CreateEntry(metadataFilename, CompressionLevel.Optimal);
                using(var metadataStream = new MemoryStream(Encoding.Unicode.GetBytes(metadata)))
                using(var entryStream = metadataEntry.Open())
                {
                    await metadataStream.CopyToAsync(entryStream);
                }
            }

            outputStream.Position = 0;

            _logger.LogInformation("Compressed {0} from {1} to {2}, removing {3:0.00}%", filenameOnly, inputFileLength, outputStream.Length, (float)((inputFileLength - outputStream.Length) * 100) / inputFileLength);
            
            return outputStream;
        }
    }
}