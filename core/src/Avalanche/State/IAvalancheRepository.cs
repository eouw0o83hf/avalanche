using System;
using System.Data;
using Avalanche.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Framework.Logging;

namespace Avalanche.State
{
    /// <summary>
    /// Encapsulates the state file which knows what has been
    /// sent to glacier
    /// </summary>
    public interface IAvalancheRepository
    {
        int GetOrCreateVaultId(string name, string region);
        int GetOrCreateCatalogId(string filename, string uniqueId);
        bool FileIsArchived(Guid fileId);
        void MarkFileAsArchived(ArchivedPictureModel model, string vaultName, string vaultRegion, string catalogFilename, string catalogUniqueId);
    }
    
    public class AvalancheRepository : IAvalancheRepository
    {
        //TODO:
        //- Convert raw SQL into EF since it can do sqlite now
        //- Auto-detect and create catalog
        //-- pull catalog id
        //- bind pictures to vaults
        //-- autodetect vaults
        //-- pull vaultid

        private readonly ILogger<AvalancheRepository> _logger;
        private readonly IDbConnection _db;

        // In test mode, no information should be persisted
        private readonly bool _testMode;

        // For non-test applications, `db` is expected
        // to be a SqliteConnection, since the Lightroom
        // catalog is a sqlite database
        public AvalancheRepository(ILogger<AvalancheRepository> logger, IDbConnection db, bool testMode)
        {
            _db = db;
            _testMode = testMode;
            _logger = logger;

            AssertDatabaseExists();
        }

        private const string CreateCatalogsCommand = @"
CREATE TABLE IF NOT EXISTS Catalogs
(
    CatalogId INTEGER NOT NULL
        CONSTRAINT PK_Catalogs
            PRIMARY KEY 
            AUTOINCREMENT,
    UniqueId NVARCHAR(200) NOT NULL
        CONSTRAINT IX_Catalogs_UniqueId
            UNIQUE
            ON CONFLICT ROLLBACK,
    FileName NVARCHAR(200) NOT NULL
)";

        private const string CreateVaultsCommand = @"
CREATE TABLE IF NOT EXISTS Vaults
(
    VaultId INTEGER NOT NULL
        CONSTRAINT PK_Vaults
            PRIMARY KEY
            AUTOINCREMENT,
    Name NVARCHAR(200) NOT NULL
        CONSTRAINT IX_Vaults_UniqueId
            UNIQUE
            ON CONFLICT ROLLBACK,
    Region NVARCHAR(200) NOT NULL
)";

        private const string CreatePicturesCommand = @"
CREATE TABLE IF NOT EXISTS Pictures
(
    PictureId INTEGER NOT NULL
        CONSTRAINT PK_Pictures
            PRIMARY KEY
            AUTOINCREMENT,
    CatalogId INTEGER NOT NULL
        CONSTRAINT FK_Pictures_Catalogs
            REFERENCES Catalogs(CatalogId),
    VaultId INTEGER NOT NULL
        CONSTRAINT FK_Pictures_Vaults
            REFERENCES Vaults(VaultId),
    FileAbsolutePath TEXT NOT NULL,
    FileCatalogPath TEXT NULL,
    FileName TEXT NOT NULL,
    FileId UNIQUEIDENTIFIER NOT NULL,
    ImageId UNIQUEIDENTIFIER NOT NULL,
    GlacierArchiveId TEXT NULL,
    GlacierHttpStatusCode INT NULL,
    GlacierLocation TEXT NULL,
    GlacierMetadata TEXT NULL,
    GlacierTimestamp DATETIME NULL
)";

        private int ExecuteNonQuery(string sql)
        {
            using(var command = _db.CreateCommand())
            {
                command.CommandText = sql;
                return command.ExecuteNonQuery();
            }
        }

        private void AssertDatabaseExists()
        {
            if(_testMode)
            {
                _logger.LogInformation("Not asserting Avalanche DB since we're in test mode");
                return;
            }
            
            ExecuteNonQuery(CreateCatalogsCommand);
            ExecuteNonQuery(CreateVaultsCommand);
            ExecuteNonQuery(CreatePicturesCommand);
        }

private const string SelectVaultId = @"
SELECT
    v.VaultId
FROM
    Vaults v
WHERE
    v.Name = $name
";

        public int GetOrCreateVaultId(string name, string region)
        {
            using (var query = _db.CreateCommand())
            {
                query.CommandText = SelectVaultId;

                query.Parameters.Add(new SqliteParameter("$name", name));
                var response = query.ExecuteScalar();

                if (response != null)
                {
                    return (int)(long)response;
                }
            }

            // Nothing exists yet, create it
            return CreateVault(name, region);
        }

        private const string InsertVault = @"
INSERT INTO Vaults
(
    Name,
    Region
)
VALUES
(
    $name,
    $region
)";

        private int CreateVault(string name, string region)
        {
            if(_testMode)
            {
                return -1;
            }
            
            using (var command = _db.CreateCommand())
            {                
                command.CommandText = InsertVault;
                command.Parameters.Add(new SqliteParameter("$name", name));
                command.Parameters.Add(new SqliteParameter("$region", region));
                command.ExecuteNonQuery();

                command.CommandText = @"SELECT LAST_INSERT_ROWID()";
                var responseId = (long)command.ExecuteScalar();
                return (int)responseId;
            }
        }

        private const string SelectCatalogId = @"
SELECT
    c.CatalogId
FROM
    Catalogs c
WHERE
    c.UniqueId = $uniqueId
";

        public int GetOrCreateCatalogId(string filename, string uniqueId)
        {
            using (var query = _db.CreateCommand())
            {
                query.CommandText = SelectCatalogId;

                query.Parameters.Add(new SqliteParameter("$uniqueId", uniqueId));
                var response = query.ExecuteScalar();

                if (response != null)
                {
                    return (int)(long)response;
                }
            }

            // Nothing exists yet, create it
            return CreateCatalog(filename, uniqueId);
        }

        private const string InsertCatalog = @"
INSERT INTO Catalogs
(
    UniqueId,
    FileName
)
VALUES
(
    $uniqueId,
    $filename
)";

        private int CreateCatalog(string filename, string uniqueId)
        {
            if(_testMode)
            {
                return -1;
            }
            
            using (var command = _db.CreateCommand())
            {
                command.CommandText = InsertCatalog;
                command.Parameters.Add(new SqliteParameter("$uniqueId", uniqueId));
                command.Parameters.Add(new SqliteParameter("$filename", filename));
                command.ExecuteNonQuery();

                command.CommandText = @"SELECT LAST_INSERT_ROWID()";
                var responseId = (long)command.ExecuteScalar();
                return (int)responseId;
            }
        }

        private const string SelectPictureCount = @"
SELECT
    COUNT(*)
FROM
    Pictures p
WHERE
    p.FileId = $fileId
";

        public bool FileIsArchived(Guid fileId)
        {
            using(var query = _db.CreateCommand())
            {
                query.CommandText = SelectPictureCount;
                query.Parameters.Add(new SqliteParameter("$fileId", fileId));
                var result = query.ExecuteScalar();
                return (long)result > 0;
            }
        }

        private const string InsertPicture = @"
INSERT INTO Pictures
(
    CatalogId,
    VaultId,
    FileAbsolutePath, 
    FileCatalogPath,
    FileName,
    FileId,
    ImageId,
    GlacierArchiveId,
    GlacierHttpStatusCode,
    GlacierLocation,
    GlacierMetadata,
    GlacierTimestamp
)
VALUES
(
    $catalogId,
    $vaultId,
    $fileAbsolutePath,
    $fileCatalogPath,
    $fileName,
    $fileId,
    $imageId,
    $glacierArchiveId,
    $glacierHttpStatusCode,
    $glacierLocation,
    $glacierMetadata,
    $glacierTimestamp
)
";

        public void MarkFileAsArchived(ArchivedPictureModel model, string vaultName, string vaultRegion, string catalogFilename, string catalogUniqueId)
        {
            if(_testMode)
            {
                return;
            }
            
            var vaultId = GetOrCreateVaultId(vaultName, vaultRegion);
            var catalogId = GetOrCreateCatalogId(catalogFilename, catalogUniqueId);

            using (var insert = _db.CreateCommand())
            {                
                insert.CommandText = InsertPicture;

                insert.Parameters.Add(new SqliteParameter("$catalogId", catalogId));
                insert.Parameters.Add(new SqliteParameter("$vaultId", vaultId));
                insert.Parameters.Add(new SqliteParameter("$fileAbsolutePath", model.Picture.AbsolutePath));
                insert.Parameters.Add(new SqliteParameter("$fileCatalogPath", model.Picture.CatalogRelativePath));
                insert.Parameters.Add(new SqliteParameter("$fileName", model.Picture.FileName));
                insert.Parameters.Add(new SqliteParameter("$fileId", model.Picture.FileId));
                insert.Parameters.Add(new SqliteParameter("$imageId", model.Picture.ImageId));
                insert.Parameters.Add(new SqliteParameter("$glacierArchiveId", model.Archive.ArchiveId));
                insert.Parameters.Add(new SqliteParameter("$glacierHttpStatusCode", (int)model.Archive.Status));
                insert.Parameters.Add(new SqliteParameter("$glacierLocation", model.Archive.Location));
                insert.Parameters.Add(new SqliteParameter("$glacierMetadata", model.Archive.Metadata));
                insert.Parameters.Add(new SqliteParameter("$glacierTimestamp", model.Archive.PostedTimestamp));

                insert.ExecuteNonQuery();
            }
        }
    }
}
