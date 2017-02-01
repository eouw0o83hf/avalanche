using System;
using Xunit;
using Avalanche.Glacier;
using System.IO;
using NSubstitute;
using Microsoft.Framework.Logging;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.IO.Compression;

namespace Avalanche.Tests.Glacier
{
    public class ArchiveProviderTests : IDisposable
    {
        private readonly ILogger<ArchiveProvider> _logger;
        private readonly ArchiveProvider _sut;
        
        private readonly string _imageData;
        private string _filename;
        private string _filePath;
        private readonly string _metadata;

        public ArchiveProviderTests()
        {
            _logger = Substitute.For<ILogger<ArchiveProvider>>();
            _sut = new ArchiveProvider(_logger);

            _imageData = string.Join(Environment.NewLine, 
                            Enumerable.Range(0, 10000)
                                    .Select(a => Guid.NewGuid()));
            WriteImageFile($"{Guid.NewGuid()}.jpg");

            _metadata = $"Additional information about {_filename}";
        }

        private void WriteImageFile(string filename)
        {
            _filename = filename;
            _filePath = Path.Combine(Path.GetTempPath(), _filename);
            File.WriteAllText(_filePath, _imageData);
        }
        
        public void Dispose()
        {
            File.Delete(_filePath);
        }
        
        // Not a unit test, but manual test to make sure you can
        // manually open the file on the filesystem
        // [Fact]
        public async Task WriteArchiveToDisk() 
        {
            using(var outputStream = await _sut.GetFileStream(_filePath, _metadata))
            using(var stream = File.OpenWrite($"/code/tmp/{_filename}.zip"))
            {
                Assert.NotNull(outputStream);
                await outputStream.CopyToAsync(stream);
            }
        }

        [Fact]
        public async Task ArchivedDataIsCompressed()
        {
            using(var outputStream = await _sut.GetFileStream(_filePath, _metadata))
            {
                var dataLength = Encoding.UTF8.GetBytes(_imageData).Length;
                Assert.True(outputStream.Length < dataLength);
            }
        }

        [Fact]
        public async Task ArchiveContainsImageAndMetadata()
        {
            using(var outputStream = await _sut.GetFileStream(_filePath, _metadata))
            using(var responseArchive = new ZipArchive(outputStream))
            {
                Assert.Equal(2, responseArchive.Entries.Count);
                var containsImage = responseArchive.Entries.Any(a => a.Name == _filename);
                Assert.True(containsImage);

                var metadataName = $"metadata.txt";
                var containsMetadata = responseArchive.Entries.Any(a => a.Name == metadataName);
                Assert.True(containsMetadata);
            }
        }

        [Fact]
        public async Task MetadataRenamedOnNameCollision()
        {
            WriteImageFile("metadata.txt");
            using(var outputStream = await _sut.GetFileStream(_filePath, _metadata))
            using(var responseArchive = new ZipArchive(outputStream))
            {
                Assert.Equal(2, responseArchive.Entries.Count);
                var containsImage = responseArchive.Entries.Any(a => a.Name == _filename);
                Assert.True(containsImage);

                var newMetadataName = $"{_filename}.actuallythemetadata.txt";
                var containsMetadata = responseArchive.Entries.Any(a => a.Name == newMetadataName);
                Assert.True(containsMetadata);
            }
        }
    }
}