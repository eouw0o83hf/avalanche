using System;
using System.Linq;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;
using Microsoft.Framework.Logging;
using Xunit;
using NSubstitute;
using Avalanche.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Avalanche.Tests.Runner
{
    public class AvalancheRunnerTests
    {
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly IGlacierGateway _glacier;
        private readonly ILightroomReader _lightroom;
        private readonly IAvalancheRepository _avalanche;
        private readonly ExecutionParameters _parameters;

        private readonly AvalancheRunner _sut;

        public AvalancheRunnerTests()
        {
            _logger = Substitute.For<ILogger<AvalancheRunner>>();
            _glacier = Substitute.For<IGlacierGateway>();
            _lightroom = Substitute.For<ILightroomReader>();
            _avalanche = Substitute.For<IAvalancheRepository>();
            _parameters = new ExecutionParameters
            {
                Glacier = new GlacierParameters
                {
                },
                Avalanche = new AvalancheParameters
                {
                }
            };

            _sut = new AvalancheRunner(_logger, _glacier, _lightroom, _avalanche, _parameters);
        }

        [Fact]
        public async Task Archiving_GivenPicturesAndExisting_FiltersOnLibraryCount()
        {
            var pictures = Enumerable
                            .Range(0, 10)
                            .Select(a => new PictureModel
                                    {
                                        AbsolutePath = $"/dev/null/image{a}.jpg",
                                        FileName = $"{a}.jpg",
                                        FileId = Guid.NewGuid(),
                                        ImageId = Guid.NewGuid(),
                                        LibraryCount = a / 5 // First five = 0, next five = 1
                                    })
                            .ToList();

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);
            _avalanche.FileIsArchived(Arg.Any<Guid>()).Returns(false);

            var calledFileIds = new HashSet<Guid>();
            _glacier.SaveImage(Arg.Do<PictureModel>(a => calledFileIds.Add(a.FileId)), Arg.Any<string>())
                    .Returns(new ArchivedPictureModel());

            await _sut.Run();

            var expectedArchivedFileIds = pictures
                                            .Where(a => a.LibraryCount > 0)
                                            .Select(a => a.FileId);
            Assert.Equal(expectedArchivedFileIds, calledFileIds);
        }

        [Fact]
        public async Task Archiving_GivenPicturesAndExisting_FiltersOnExistingArchive()
        {
            var pictures = Enumerable
                            .Range(0, 10)
                            .Select(a => new PictureModel
                                    {
                                        AbsolutePath = $"/dev/null/image{a}.jpg",
                                        FileName = $"{a}.jpg",
                                        FileId = Guid.NewGuid(),
                                        ImageId = Guid.NewGuid(),
                                        LibraryCount = 1
                                    })
                            .ToList();

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);

            var existingArchivedFileIds = pictures
                                            .Take(pictures.Count / 2)
                                            .Select(a => a.FileId)
                                            .ToList();
            var expectedArchivedFileIds = pictures
                                            .Select(a => a.FileId)
                                            .Except(existingArchivedFileIds);
            _avalanche.FileIsArchived(Arg.Any<Guid>())
                .ReturnsForAnyArgs(a => existingArchivedFileIds.Contains(a.Arg<Guid>()));

            var calledFileIds = new HashSet<Guid>();
            _glacier.SaveImage(Arg.Do<PictureModel>(a => calledFileIds.Add(a.FileId)), Arg.Any<string>())
                    .Returns(new ArchivedPictureModel());

            await _sut.Run();

            Assert.Equal(expectedArchivedFileIds, calledFileIds);
        }

        [Fact]
        public void Archiving_GivenTransportFailures_TriesThreeTimes()
        {
            Assert.False(true);
        }

        [Fact]
        public void Archiving_GivenTransportFailures_GivesUpAfterThreeFailures()
        {
            Assert.False(true);            
        }
    }
}