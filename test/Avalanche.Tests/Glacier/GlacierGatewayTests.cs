using Xunit;
using Avalanche.Glacier;
using System.IO;
using NSubstitute;
using Microsoft.Framework.Logging;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Glacier;
using Amazon.Glacier.Model;
using System.Collections.Generic;
using System.Net;
using Avalanche.Models;
using Amazon.Runtime;

namespace Avalanche.Tests.Glacier
{
    // There is very divergent behavior in some methods depending on whether or not
    // test mode is activated. Since we wire up the sut in context, it's best to just
    // subclass out the test-mode-ness
    public abstract class GlacierGatewayTestBase
    {
        protected readonly ILogger<GlacierGateway> _logger;
        protected readonly IAmazonGlacier _glacier;
        protected readonly IConsolePercentUpdater _updater;
        protected readonly IArchiveProvider _archiveProvider;

        protected readonly GlacierGateway _sut;
        protected const string PreexistingVaultName = "pre-existing vault name";
        
        protected GlacierGatewayTestBase(bool testMode)
        {
            _logger = Substitute.For<ILogger<GlacierGateway>>();
            _glacier = Substitute.For<IAmazonGlacier>();
            _updater = Substitute.For<IConsolePercentUpdater>();
            _archiveProvider = Substitute.For<IArchiveProvider>();

            // We need to initialize the MemoryStream with some data so that
            // the sanity checks don't fail
            var bytes = Enumerable.Range(0, 100).Select(a => (byte)a).ToArray();
            _archiveProvider.GetFileStream(Arg.Any<string>(), Arg.Any<string>()).Returns(new MemoryStream(bytes));

            _sut = new GlacierGateway(_glacier, _logger, _updater, _archiveProvider, null, testMode);

            _glacier.ListVaultsAsync(Arg.Any<ListVaultsRequest>())
                    .Returns(new ListVaultsResponse
                    {
                        VaultList = new List<DescribeVaultOutput>
                        {
                            new DescribeVaultOutput
                            {
                                VaultName = PreexistingVaultName
                            }
                        }
                    });
            _glacier.CreateVaultAsync(Arg.Any<CreateVaultRequest>())
                    .Returns(new CreateVaultResponse
                    {
                        HttpStatusCode = HttpStatusCode.OK
                    });
        }
    }

    public class NonTestModeGlacierGatewayTestBase : GlacierGatewayTestBase
    {
        public NonTestModeGlacierGatewayTestBase() : base(false) { }

        [Fact]
        public async Task AssertVaultExists_GivenExistingVault_DoesNotCreate()
        {
            await _sut.AssertVaultExists(PreexistingVaultName);
            await _glacier.DidNotReceiveWithAnyArgs().CreateVaultAsync(Arg.Any<CreateVaultRequest>());
        }

        [Fact]
        public async Task AssertVaultExists_GivenEmptyVault_CreatesNew()
        {
            await _sut.AssertVaultExists("new vault name");
            await _glacier.ReceivedWithAnyArgs().CreateVaultAsync(Arg.Any<CreateVaultRequest>());
        }

        [Fact]
        public async Task AssertVaultExists_GivenPaddedName_TrimsInputName()
        {
            await _sut.AssertVaultExists("  padded  ");
            await _glacier.Received().CreateVaultAsync(Arg.Is<CreateVaultRequest>(a => a.VaultName.Equals("padded")));
        }

        [Fact]
        public async Task SaveFileWithMetadata_WhenUpdated_CallsToPercentUpdater()
        {
            var picture = new PictureModel
            {
                AbsolutePath = "/dev/null",
                FileName = "notafile.jpg"
            };

            // In this test, we fire off SaveImage() below, but the bulk
            // of the work is done here, to assert that *during* the call
            // to UploadArchiveAsync(), the callback to the console updater
            // is handled correctly
            _glacier.UploadArchiveAsync(Arg.Any<UploadArchiveRequest>())
                    .ReturnsForAnyArgs(a => 
                    {
                        var request = a.Arg<UploadArchiveRequest>();
                        var callback = request.StreamTransferProgress;

                        // The call initializes the updater to 0
                        _updater.Received().UpdatePercentage(Arg.Any<string>(), Arg.Is(0));

                        // Hit it with an update
                        callback.Invoke(null, new StreamTransferProgressArgs(50, 50, 100));

                        // Verify that the percentage came back
                        _updater.Received().UpdatePercentage(Arg.Any<string>(), Arg.Is(50));

                        return new UploadArchiveResponse
                        {
                            HttpStatusCode = HttpStatusCode.OK,
                            ArchiveId = "archive ID"
                        };
                    });


            await _sut.SaveImage(picture);
            await _glacier.ReceivedWithAnyArgs().UploadArchiveAsync(Arg.Any<UploadArchiveRequest>());
        }
    }

    public class TestModeGlacierGatewayTestBase : GlacierGatewayTestBase
    {
        public TestModeGlacierGatewayTestBase() : base(true) { }

        [Fact]
        public async Task AssertVaultExists_GivenEmptyVault_DoesNotCreateNew()
        {
            await _sut.AssertVaultExists("new vault name");
            await _glacier.DidNotReceiveWithAnyArgs().CreateVaultAsync(Arg.Any<CreateVaultRequest>());
        }

        [Fact]
        public async Task SaveFileWithMetadata_DoesNotPushToAws()
        {
            var picture = new PictureModel
            {
                AbsolutePath = "/dev/null",
                FileName = "notafile.jpg"
            };

            await _sut.SaveImage(picture);
            await _glacier.DidNotReceiveWithAnyArgs().UploadArchiveAsync(Arg.Any<UploadArchiveRequest>());
        }
    }
}