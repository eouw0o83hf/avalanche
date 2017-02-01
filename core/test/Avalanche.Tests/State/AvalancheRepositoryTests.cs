using System;
using System.Data;
using Xunit;
using Avalanche.State;
using NSubstitute;
using Microsoft.Data.Sqlite;
using Avalanche.Models;
using System.Net;
using Microsoft.Framework.Logging;

namespace Avalanche.Tests.State
{
    public abstract class AvalancheRepositoryTestBase
    {
        protected readonly ILogger<AvalancheRepository> _logger;
        protected readonly IDbConnection _db;
        protected readonly AvalancheRepository _sut;

        protected AvalancheRepositoryTestBase(bool testMode)
        {
            _logger = Substitute.For<ILogger<AvalancheRepository>>();
            
            // If this test class gets much more complex, this base class
            // should probably be abandoned and the test-mode vs. non-test-mode
            // classes can just diverge entirely
            if(testMode)
            {
                var command = Substitute.For<IDbCommand>();
                // The methods that read will be tested with 0-count results
                command.ExecuteScalar().Returns(0L);
                
                _db = Substitute.For<IDbConnection>();
                _db.CreateCommand().Returns(command);
            }
            else
            {
                // In-memory sqlite db
                _db = new SqliteConnection("Data Source=:memory:");
                _db.Open();
            }

            _sut = new AvalancheRepository(_logger, _db, testMode);
        }
    }

    public class NonTestModeAvalancheRepositoryTests : AvalancheRepositoryTestBase
    {
        public NonTestModeAvalancheRepositoryTests() : base(false) { }
        
        [Fact]
        public void OnStartup_GivenEmptyDatabase_CreatesTables()
        {
            // The test condition is setup by the constructor, we just
            // have to verify that it did what it's supposed to

            using(var command = _db.CreateCommand())
            {
                command.CommandText = @"SELECT COUNT(*) FROM Catalogs";
                var result = (long)command.ExecuteScalar();
                Assert.Equal(0, result);
                
                command.CommandText = @"SELECT COUNT(*) FROM Vaults";
                result = (long)command.ExecuteScalar();
                Assert.Equal(0, result);

                command.CommandText = @"SELECT COUNT(*) FROM Pictures";
                result = (long)command.ExecuteScalar();
                Assert.Equal(0, result);
            }
        }

        [Fact]
        public void OnStartup_GivenPopulatedDatabase_DoesNotReCreateTables()
        {
            // The schema will already be populated by the constructor, so
            // just creating a new test object will trigger this test condition.
            var duplicate = new AvalancheRepository(_logger, _db, false);

            // xunit doesn't have an Assert.DoesNotThrow(), so there's no actual
            // assertion in this test. The failure condition is an exception.
        }

        [Fact]
        public void GetOrCreateVaultId_GivenEmptyTable_CreatesNewId()
        {
            var result = _sut.GetOrCreateVaultId("name", "region");
            Assert.True(result > 0);
        }

        [Fact]
        public void GetOrCreateVaultId_GivenPopulatedTable_ReturnsExisting()
        {
            var initialId = _sut.GetOrCreateVaultId("name", "region");
            var finalId = _sut.GetOrCreateVaultId("name", "region");
            Assert.Equal(initialId, finalId);
        }

        [Fact]
        public void GetOrCreateVaultId_GivenDifferentData_ReturnsDifferentId()
        {
            var initialId = _sut.GetOrCreateVaultId("name", "region");
            var finalId = _sut.GetOrCreateVaultId("different name", "different region");
            Assert.NotEqual(initialId, finalId);
        }

        [Fact]
        public void GetOrCreateCatalogId_GivenEmptyTable_CreatesNewId()
        {
            var result = _sut.GetOrCreateCatalogId("filename", "uniqueid");
            Assert.True(result > 0);
        }

        [Fact]
        public void GetOrCreateCatalogId_GivenPopulatedTable_ReturnsExisting()
        {
            var initialId = _sut.GetOrCreateCatalogId("filename", "uniqueid");
            var finalId = _sut.GetOrCreateCatalogId("filename", "uniqueid");
            Assert.Equal(initialId, finalId);
        }

        [Fact]
        public void GetOrCreateCatalogId_GivenPopulatedTable_ReturnsDifferentId()
        {
            var initialId = _sut.GetOrCreateCatalogId("filename", "uniqueid");
            var finalId = _sut.GetOrCreateCatalogId("different filename", "different uniqueid");
            Assert.NotEqual(initialId, finalId);
        }

        [Fact]
        public void FileIsArchived_GivenEmptyTable_SaysNo()
        {
            var result = _sut.FileIsArchived(Guid.NewGuid());
            Assert.False(result);
        }

        [Fact]
        public void FileIsArchived_GivenPopulatedTable_SaysYes()
        {
            var archiveModel = new ArchivedPictureModel
            {
                Archive = new ArchiveModel
                {
                    ArchiveId = Guid.NewGuid().ToString(),
                    Status = HttpStatusCode.OK,
                    Location = "here",
                    PostedTimestamp = DateTime.MinValue,
                    Metadata = "idk some metadata"
                },
                Picture = new PictureModel
                {
                    AbsolutePath = "/dev/null/pictures/picture.jpg",
                    CatalogRelativePath = "pictures/picture.jpg",
                    FileName = "picture.jpg",
                    FileId = Guid.NewGuid(),
                    ImageId = Guid.NewGuid(),
                    LibraryCount = 3
                }
            };
            _sut.MarkFileAsArchived(archiveModel, "vault", "region", "catalog", "catalogUniqueId");
            var result = _sut.FileIsArchived(archiveModel.Picture.FileId);
            Assert.True(result);
        }
    }

    public class TestModeAvalancheRepositoryTests : AvalancheRepositoryTestBase
    {
        public TestModeAvalancheRepositoryTests() : base(true) { }

        [Fact]
        public void OnStartup_GivenEmptyDatabase_DoesNotCreateTables()
        {
            _db.DidNotReceiveWithAnyArgs().CreateCommand();
        }

        [Fact]
        public void GetOrCreateVault_DoesNotCreateNewVault()
        {
            _sut.GetOrCreateVaultId("name", "region");
            // A command will be created once for the read, but
            // a second time for the write. Make sure we only get one.
            _db.Received(1).CreateCommand();
        }

        [Fact]
        public void GetOrCreateCatalog_DoesNotCreateNewCatalog()
        {
            _sut.GetOrCreateCatalogId("filename", "uniqueid");
            // A command will be created once for the read, but
            // a second time for the write. Make sure we only get one.
            _db.Received(1).CreateCommand();
        }

        [Fact]
        public void MakrFileAsArchive_DoesNothing()
        {
            _sut.MarkFileAsArchived(null, null, null, null, null);
            // This time, the DB shouldn't have been hit at all
            _db.DidNotReceiveWithAnyArgs().CreateCommand();
        }
    }
}