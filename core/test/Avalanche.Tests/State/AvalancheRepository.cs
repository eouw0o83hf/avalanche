using System;
using System.Data;
using Xunit;
using Avalanche.State;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Avalanche.Models;
using System.Net;

namespace Avalanche.Tests.State
{
    public class AvalancheRepositoryTests
    {
        private readonly IDbConnection _db;
        private readonly AvalancheRepository _sut;

        public AvalancheRepositoryTests()
        {
            // In-memory sqlite db
            _db = new SqliteConnection("Data Source=:memory:");
            _db.Open();
            _sut = new AvalancheRepository(_db);
        }

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
            var duplicate = new AvalancheRepository(_db);

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
}