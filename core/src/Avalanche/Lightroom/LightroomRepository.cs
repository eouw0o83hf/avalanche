using Avalanche.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Avalanche.Lightroom
{
    public class LightroomRepository
    {
        protected readonly string _filename;

        public LightroomRepository(string filename)
        {
            _filename = filename;
        }

        protected DbConnection OpenNewConnection()
        {
            var connection = new SqliteConnection(string.Format("DataSource={0};Version=3;", _filename));
            connection.Open();

            return connection;
        }

        public Guid GetUniqueId()
        {
            using (var connection = OpenNewConnection())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
SELECT
	a.value
FROM
	Adobe_variablesTable a
WHERE
	a.name = 'Adobe_storeProviderID'
";
                var result = (string)command.ExecuteScalar();
                return result.ParseGuid().Value;
            }
        }

        public ICollection<PictureModel> GetAllPictures()
        {
            var result = new List<PictureModel>();
            using(var connection = OpenNewConnection())
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
SELECT
	r.absolutePath AS 'AbsolutePath',
	r.relativePathFromCatalog AS 'PathFromCatalog',
	l.pathFromRoot AS 'PathFromLibraryRoot',
	f.idx_filename AS 'Filename',    
	SUM(CASE WHEN i.id_local IS NOT NULL THEN 1 ELSE 0 END) AS 'CollectionCount',
	m.id_global AS 'ImageId',
	f.id_global AS 'FileId'
FROM
	AgLibraryFile f
	INNER JOIN AgLibraryFolder l ON f.folder = l.id_local
	INNER JOIN AgLibraryRootFolder r ON l.rootFolder = r.id_local
	LEFT OUTER JOIN Adobe_images m ON f.id_local = m.rootFile
	LEFT OUTER JOIN AgLibraryCollectionImage i ON m.id_local = i.image
	LEFT OUTER JOIN AgLibraryCollection c ON i.collection = c.id_local
GROUP BY
	r.absolutePath,
	r.relativePathFromCatalog,
	l.pathFromRoot,
	f.idx_filename,
	m.id_global,
	f.id_global
";

                var reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                while (reader.Read())
                {
                    result.Add(ToModel(reader));
                }

                connection.Close();
            }

            return result;
        }

        protected PictureModel ToModel(IDataRecord record)
        {            
            var relativeRoot = record["PathFromCatalog"] as string;
            var absoluteRoot = record["AbsolutePath"] as string;
            var libraryPath = record["PathFromLibraryRoot"] as string;

            return new PictureModel
            {
                AbsolutePath = Path.Combine(absoluteRoot, libraryPath),
                CatalogRelativePath = relativeRoot == null ? "NULL" : Path.Combine(relativeRoot, libraryPath),
                FileName = record.GetString(3),
                ImageId = record.GetGuid(5),
                FileId = record.GetGuid(6),
                LibraryCount = record.GetInt32(4)
            };
        }
    }
}
