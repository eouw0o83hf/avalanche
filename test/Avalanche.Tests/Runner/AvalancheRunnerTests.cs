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
using NSubstitute.Core;

namespace Avalanche.Tests.Runner
{
    public class AvalancheRunnerTests
    {
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly IGlacierGateway _glacier;
        private readonly ILightroomReader _lightroom;
        private readonly IAvalancheRepository _avalanche;
        private readonly ExecutionParameters _parameters;

        private readonly IInjectionFactory<IGlacierGateway> _glacierFactory;
        private readonly IInjectionFactory<ILightroomReader> _lightroomFactory;
        private readonly IInjectionFactory<IAvalancheRepository> _avalancheFactory;

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

            _glacierFactory = Substitute.For<IInjectionFactory<IGlacierGateway>>();
            _glacierFactory.Create().Returns(_glacier);

            _lightroomFactory = Substitute.For<IInjectionFactory<ILightroomReader>>();
            _lightroomFactory.Create().Returns(_lightroom);

            _avalancheFactory = Substitute.For<IInjectionFactory<IAvalancheRepository>>();
            _avalancheFactory.Create().Returns(_avalanche);

            _sut = new AvalancheRunner(_logger, _glacierFactory, _lightroomFactory, _avalancheFactory, _parameters);
        }

        [Fact]
        public async Task Archiving_BeforeUploadingFiles_AssertsVaultExists()
        {
            // Cause the actual image call to fail hard and stop execution.
            // This will allow us to assert that the call to vault assertion
            // occurred prior to attempts to save images.
            _glacier.SaveImage(Arg.Any<PictureModel>(), Arg.Any<string>()).Returns(SaveImageFails);

            try
            {
                await _sut.Run();
            }
            catch
            {
                // This will fail because of the wireup, nothing to see here
            }

            await _glacier.ReceivedWithAnyArgs(1).AssertVaultExists(Arg.Any<string>());
        }

        [Fact]
        public async Task Archiving_GivenPicturesAndExisting_GroupsByFile()
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
            pictures.AddRange(pictures);

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);
            _avalanche.FileIsArchived(Arg.Any<Guid>()).Returns(false);

            var calledFileIds = new HashSet<Guid>();
            _glacier.SaveImage(Arg.Do<PictureModel>(a => calledFileIds.Add(a.FileId)), Arg.Any<string>())
                    .Returns(new ArchivedPictureModel());

            await _sut.Run();

            var expectedArchivedFileIds = pictures
                                            .GroupBy(a => a.FileId)
                                            .Select(a => a.Key);
            Assert.Equal(expectedArchivedFileIds, calledFileIds);
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
        public async Task Archiving_GivenTransportFailures_TriesThreeTimes()
        {
            var pictures = new []
            {
                new PictureModel
                {
                    AbsolutePath = $"/dev/null/0.jpg",
                    FileName = $"0.jpg",
                    FileId = Guid.NewGuid(),
                    ImageId = Guid.NewGuid(),
                    LibraryCount = 1
                }
            };

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);
            _avalanche.FileIsArchived(Arg.Any<Guid>()).ReturnsForAnyArgs(false);

            _glacier.SaveImage(Arg.Any<PictureModel>(), Arg.Any<string>())
                    .Returns(SaveImageFails, SaveImageFails, SaveImageSucceeds);

            var result = await _sut.Run();

            _avalanche.Received()
                      .MarkFileAsArchived(Arg.Any<ArchivedPictureModel>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
            Assert.Equal(1, result.Successes.Count);
        }

        [Fact]
        public async Task Archiving_GivenTransportFailures_GivesUpAfterThreeFailures()
        {
            var pictures = new []
            {
                new PictureModel
                {
                    AbsolutePath = $"/dev/null/0.jpg",
                    FileName = $"0.jpg",
                    FileId = Guid.NewGuid(),
                    ImageId = Guid.NewGuid(),
                    LibraryCount = 1
                }
            };

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);
            _avalanche.FileIsArchived(Arg.Any<Guid>()).ReturnsForAnyArgs(false);

            _glacier.SaveImage(Arg.Any<PictureModel>(), Arg.Any<string>())
                    .Returns(SaveImageFails, SaveImageFails, SaveImageFails);

            var result = await _sut.Run();

            _avalanche.DidNotReceive()
                      .MarkFileAsArchived(Arg.Any<ArchivedPictureModel>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
            Assert.Equal(1, result.Failures.Count);       
        }

        private const int MaximumDegreeOfParallelism = 3;

        [Fact]
        public async Task Archiving_GivenQueue_ExecutesExactlynThreeTasksInParallel()
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

            _avalanche.FileIsArchived(Arg.Any<Guid>()).ReturnsForAnyArgs(false);            

            var processingFileIds = new HashSet<Guid>();
            var maxDegreeOfParallelismReached = 0;

            // Here we back up inbound parallelism until the max is
            // reached, then wait a little longer to make sure that
            // no extra prallel runs occur
            var maxFilesAreProcessingTask = Task.Run(async () =>
            {
                // This will block once until the maximum count is reached,
                // then the Task's completion will allow further rounds to
                // proceed immediately
                while(processingFileIds.Count < MaximumDegreeOfParallelism)
                {
                    await Task.Delay(10);
                }
                await Task.Delay(30);
            });

            _glacier.SaveImage(Arg.Any<PictureModel>(), Arg.Any<string>())
                    .Returns(async a =>
                    {
                        var fileId = a.Arg<PictureModel>().FileId;
                        processingFileIds.Add(fileId);

                        if(processingFileIds.Count > maxDegreeOfParallelismReached)
                        {
                            maxDegreeOfParallelismReached = processingFileIds.Count;
                        }

                        await maxFilesAreProcessingTask;
                        
                        processingFileIds.Remove(fileId);
                        return new ArchivedPictureModel
                        {
                            Archive = new ArchiveModel
                            {
                            },
                            Picture = new PictureModel
                            {                                
                            }
                        };
                    });

            await _sut.Run();

            Assert.Equal(MaximumDegreeOfParallelism, maxDegreeOfParallelismReached);
        }

        [Fact]
        public async Task Archiving_GivenOneInQueue_AwaitsCompletion()
        {
            var pictures = new [] 
            {
                new PictureModel
                {
                    AbsolutePath = $"/dev/null/image.jpg",
                    FileName = $"file.jpg",
                    FileId = Guid.NewGuid(),
                    ImageId = Guid.NewGuid(),
                    LibraryCount = 1
                }
            };

            _lightroom.GetCatalogId().Returns(Guid.NewGuid());
            _lightroom.GetAllPictures().Returns(pictures);

            _avalanche.FileIsArchived(Arg.Any<Guid>()).ReturnsForAnyArgs(false);            

            var taskCompleted = false;
            _glacier.SaveImage(Arg.Any<PictureModel>(), Arg.Any<string>())
                    .Returns(async a =>
                    {
                        await Task.Delay(30);
                        taskCompleted = true;

                        return new ArchivedPictureModel
                        {
                            Archive = new ArchiveModel
                            {
                            },
                            Picture = new PictureModel
                            {                                
                            }
                        };
                    });

            await _sut.Run();
            Assert.True(taskCompleted);
        }

        private static ArchivedPictureModel SaveImageFails(CallInfo callInfo)
        {
            throw new Exception();
        } 

        private static ArchivedPictureModel SaveImageSucceeds(CallInfo callInfo) => new ArchivedPictureModel();
    }
}