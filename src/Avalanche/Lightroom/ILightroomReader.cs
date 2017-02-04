using Avalanche.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Avalanche.Lightroom
{
    /// <summary>
    /// Encapsulates the logic for reading from a Lightroom
    /// catalog data file
    /// </summary>
    public interface ILightroomReader
    {
        Guid GetCatalogId();
        IEnumerable<PictureModel> GetAllPictures();
    }
    
    public class LightroomReader : ILightroomReader
    {
        private readonly IDbConnection _db;

        // For non-test applications, `db` is expected
        // to be a SqliteConnection, since the Lightroom
        // catalog is a sqlite database
        public LightroomReader(IDbConnection db)
        {
            _db = db;
        }

        private const string SelectCatalogIdQuery = @"
SELECT
    a.value
FROM
    Adobe_variablesTable a
WHERE
    a.name = 'Adobe_storeProviderID'
";

        // This is pretty much the secret sauce of the entire project
        private const string SelectAllPicturesQuery = @"
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

        public Guid GetCatalogId()
        {
            using(var command = _db.CreateCommand())
            {
                command.CommandText = SelectCatalogIdQuery;
                var result = (string)command.ExecuteScalar();
                return result.ParseGuid().Value;
            }
        }

        public IEnumerable<PictureModel> GetAllPictures()
        {
            using(var command = _db.CreateCommand())
            {                
                command.CommandText = SelectAllPicturesQuery;

                using(var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    while (reader.Read())
                    {
                        yield return ToModel(reader);
                    }
                }
            }
        }

        // TODO: Refactor how this data is accessed to be uniform.
        // Not sure why the different indexing methods are being used
        private PictureModel ToModel(IDataRecord record)
        {            
            var relativeRoot = record["PathFromCatalog"] as string;
            var absoluteRoot = record["AbsolutePath"] as string;
            var libraryPath = record["PathFromLibraryRoot"] as string;

            return new PictureModel
            {
                AbsolutePath = Path.Combine(absoluteRoot, libraryPath),
                CatalogRelativePath = relativeRoot == null ? "NULL" : Path.Combine(relativeRoot, libraryPath),
                FileName = record.GetString(3),
                ImageId = record.GetString(5).ParseGuid().Value,
                FileId = record.GetString(6).ParseGuid().Value,
                LibraryCount = record.GetInt32(4)
            };
        }
    }
}
