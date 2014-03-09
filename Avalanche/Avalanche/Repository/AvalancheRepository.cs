using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalanche.Models;

namespace Avalanche.Repository
{
    public class AvalancheRepository
    {
        //TODO:
        //- Auto-detect and create catalog
        //-- pull catalog id
        //- bind pictures to vaults
        //-- autodetect vaults
        //-- pull vaultid

        protected readonly string _savePath;

        public AvalancheRepository(string savePath)
        {
            _savePath = savePath;

            AssertDatabaseExists();
        }

        #region Setup

        protected DbConnection OpenNewConnection()
        {
            var connection = new SQLiteConnection(string.Format("DataSource={0};Version=3;", _savePath));
            connection.Open();

            return connection;
        }

        protected void AssertDatabaseExists()
        {
            if (File.Exists(_savePath))
            {
                return;
            }

            SQLiteConnection.CreateFile(_savePath);

            using (var connection = OpenNewConnection())
            {
                var command = connection.CreateCommand();

                command.CommandText = @"
CREATE TABLE Catalogs
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
                command.ExecuteNonQuery();

                command.CommandText = @"
CREATE TABLE Vaults
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
                command.ExecuteNonQuery();

                command.CommandText = @"
CREATE TABLE Pictures
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
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region Read

        public bool FileIsArchived(Guid fileId)
        {
            using (var connection = OpenNewConnection())
            {
                var query = connection.CreateCommand();
                query.CommandText = @"
SELECT
    COUNT(*)
FROM
    Pictures p
WHERE
    p.FileId = $fileId
";
                query.Parameters.Add(new SQLiteParameter("$fileId", fileId));
                var result = query.ExecuteScalar();
                return (long)result > 0;
            }
        }

        public int GetOrCreateVaultId(string name, string region)
        {
            using (var connection = OpenNewConnection())
            {
                var query = connection.CreateCommand();
                query.CommandText = @"
SELECT
    v.VaultId
FROM
    Vaults v
WHERE
    v.Name = $name
";
                query.Parameters.Add(new SQLiteParameter("$name", name));
                var response = query.ExecuteScalar();

                if (response != null)
                {
                    return (int)(long)response;
                }
            }

            return CreateVault(name, region);
        }

        public int GetOrCreateCatalogId(string filename, string uniqueId)
        {
            using (var connection = OpenNewConnection())
            {
                var query = connection.CreateCommand();
                query.CommandText = @"
SELECT
    c.CatalogId
FROM
    Catalogs c
WHERE
    c.UniqueId = $uniqueId
";
                query.Parameters.Add(new SQLiteParameter("$uniqueId", uniqueId));
                var response = query.ExecuteScalar();

                if (response != null)
                {
                    return (int)(long)response;
                }
            }

            return CreateCatalog(filename, uniqueId);
        }

        #endregion

        #region Write

        protected int CreateVault(string name, string region)
        {
            using (var connection = OpenNewConnection())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"

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
                command.Parameters.Add(new SQLiteParameter("$name", name));
                command.Parameters.Add(new SQLiteParameter("$region", region));
                command.ExecuteNonQuery();

                command.CommandText = @"SELECT LAST_INSERT_ROWID()";
                var responseId = (long)command.ExecuteScalar();
                return (int)responseId;
            }
        }

        protected int CreateCatalog(string filename, string uniqueId)
        {
            using (var connection = OpenNewConnection())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
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
                command.Parameters.Add(new SQLiteParameter("$uniqueId", uniqueId));
                command.Parameters.Add(new SQLiteParameter("$filename", filename));
                command.ExecuteNonQuery();

                command.CommandText = @"SELECT LAST_INSERT_ROWID()";
                var responseId = (long)command.ExecuteScalar();
                return (int)responseId;
            }
        }

        public void MarkFileAsArchived(ArchivedPictureModel model, string vaultName, string vaultRegion, string catalogFilename, string catalogUniqueId)
        {
            var vaultId = GetOrCreateVaultId(vaultName, vaultRegion);
            var catalogId = GetOrCreateCatalogId(catalogFilename, catalogUniqueId);

            using (var connection = OpenNewConnection())
            {
                var insert = connection.CreateCommand();
                insert.CommandText = @"
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
SELECT
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
";
                insert.Parameters.Add(new SQLiteParameter("$catalogId", catalogId));
                insert.Parameters.Add(new SQLiteParameter("$vaultid", vaultId));
                insert.Parameters.Add(new SQLiteParameter("$fileAbsolutePath", model.Picture.AbsolutePath));
                insert.Parameters.Add(new SQLiteParameter("$fileCatalogPath", model.Picture.CatalogRelativePath));
                insert.Parameters.Add(new SQLiteParameter("$fileName", model.Picture.FileName));
                insert.Parameters.Add(new SQLiteParameter("$fileId", model.Picture.FileId));
                insert.Parameters.Add(new SQLiteParameter("$imageId", model.Picture.ImageId));
                insert.Parameters.Add(new SQLiteParameter("$glacierArchiveId", model.Archive.ArchiveId));
                insert.Parameters.Add(new SQLiteParameter("$glacierHttpStatusCode", (int)model.Archive.Status));
                insert.Parameters.Add(new SQLiteParameter("$glacierLocation", model.Archive.Location));
                insert.Parameters.Add(new SQLiteParameter("$glacierMetadata", model.Archive.Metadata));
                insert.Parameters.Add(new SQLiteParameter("$glacierTimestamp", model.Archive.PostedTimestamp));

                insert.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
