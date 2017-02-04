using System;
using System.Data;
using Xunit;
using Avalanche.Lightroom;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;

namespace Avalanche.Tests.Lightroom
{
    public class LightroomReaderTests
    {
        private readonly IDbConnection _db;
        
        private readonly LightroomReader _sut;

        public LightroomReaderTests()
        {
            _db = Substitute.For<IDbConnection>();
            _sut = new LightroomReader(_db);
        }

        private void SetupDbForScalarResponse(object response)
        {
            var command = Substitute.For<IDbCommand>();
            command.ExecuteScalar().Returns(response);

            _db.CreateCommand().Returns(command);
        }
        
        private void SetupDbForReaderResponse(IEnumerable<IDictionary<object, object>> response)
        {
            var responseEnumerator = response.GetEnumerator();
            var reader = Substitute.For<IDataReader>();

            // The lambda for Returns() forces it to execute every time, otherwise
            // the first response of `true` gets taken as the One Response for this method
            reader.Read().Returns(a => responseEnumerator.MoveNext());
            reader[Arg.Any<string>()].ReturnsForAnyArgs(a => responseEnumerator.Current[a.Arg<string>()]);
            reader.GetString(Arg.Any<int>()).ReturnsForAnyArgs(a => responseEnumerator.Current[a.Arg<int>()]);
            reader.GetInt32(Arg.Any<int>()).ReturnsForAnyArgs(a => responseEnumerator.Current[a.Arg<int>()]);

            var command = Substitute.For<IDbCommand>();
            command.ExecuteReader(Arg.Any<CommandBehavior>()).Returns(reader);

            _db.CreateCommand().Returns(command);
        }

        [Fact]
        public void GetCatalogId_GivenGuid_ReturnsValue()
        {
            var id = Guid.NewGuid();
            SetupDbForScalarResponse(id.ToString());

            var actualId = _sut.GetCatalogId();
            Assert.Equal(id, actualId);
        }

        [Fact]
        public void GetCatalogId_GivenNonGuid_Explodes()
        {
            SetupDbForScalarResponse("trolololo i'm not a guid and i'm not even pretending to be");
            Assert.ThrowsAny<Exception>(() => _sut.GetCatalogId());
        }

        [Fact]
        public void GetAllPictures_GivenNoData_ReturnsEmptyEnumerable()
        {
            SetupDbForReaderResponse(Enumerable.Empty<IDictionary<object, object>>());
            var response = _sut.GetAllPictures();
            Assert.False(response.Any());
        }

        private static IDictionary<object, object> GetResultItem(Guid imageId, Guid fileId)
        {
            var item = new Dictionary<object, object>();
            item["PathFromCatalog"] = "lightroom";
            item["AbsolutePath"] = "/dev/null";
            item["PathFromLibraryRoot"] = "raw";
            item[3] = "image.jpg";
            item[4] = 7;
            item[5] = imageId.ToString();
            item[6] = fileId.ToString();
            return item;            
        }

        [Fact]
        public void GetAllPictures_GivenSingleItem_MapsCorrectly()
        {            
            var imageId = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            var item = GetResultItem(imageId, fileId);

            SetupDbForReaderResponse(new [] { item });
            var response = _sut.GetAllPictures().ToList();
            Assert.Equal(1, response.Count);

            var responseItem = response.Single();
            Assert.Equal("/dev/null/raw", responseItem.AbsolutePath);
            Assert.Equal("lightroom/raw", responseItem.CatalogRelativePath);
            Assert.Equal("image.jpg", responseItem.FileName);
            Assert.Equal(imageId, responseItem.ImageId);
            Assert.Equal(fileId, responseItem.FileId);
            Assert.Equal(7, responseItem.LibraryCount);
        }

        [Fact]
        public void GetAllPictures_GivenMultipleItems_EnumeratesCorrectly()
        {
            var imageId = Guid.NewGuid();
            var fileId = Guid.NewGuid();
            var item = GetResultItem(imageId, fileId);
            var set = Enumerable.Range(0, 100).Select(a => item);

            SetupDbForReaderResponse(set);
            var response = _sut.GetAllPictures();
            Assert.Equal(100, response.Count());
        }
    }
}