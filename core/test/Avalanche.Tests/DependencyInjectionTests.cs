using Xunit;
using StructureMap;
using Avalanche.Glacier;
using Microsoft.Framework.Logging;
using Amazon;
using Amazon.Glacier;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;

namespace Avalanche.Tests
{
    public class DependencyInjectionTests
    {
        private readonly ExecutionParameters _executionParameters;

        private readonly IContainer _container;

        public DependencyInjectionTests()
        {
            _executionParameters = new ExecutionParameters
            {
                Avalanche = new AvalancheParameters
                {
                    CatalogFilePath = ":memory:",
                    AvalancheFilePath = ":memory:"
                },
                Glacier = new GlacierParameters
                {
                    AccountId = "test account id",
                    AccessKeyId = "key id",
                    SecretAccessKey = "such secret very wow",
                    Region = RegionEndpoint.USEast1.SystemName
                },
                CommandLineParameters = new CommandLineParameters
                {
                }
            };
            
            _container = new Container();
            new DependencyInjectionInstaller(_executionParameters).Install(_container);
        }

        [Fact]
        public void GlacierComponentsResolve()
        {
            var glacier = _container.GetInstance<IAmazonGlacier>();
            var gateway = _container.GetInstance<IGlacierGateway>();
            var logger = _container.GetInstance<ILogger<DependencyInjectionTests>>();
        }

        [Fact]
        public void LightroomComponentsResolve()
        {
            var lightroom = _container.GetInstance<ILightroomReader>();
        }

        [Fact]
        public void AvalancheStateComponentsResolve()
        {
            var avalancheState = _container.GetInstance<IAvalancheRepository>();
            var vaultId = avalancheState.GetOrCreateVaultId("name", "region");
            Assert.True(vaultId > 0);
        }

        [Fact]
        public void AvalancheRunnerResolves()
        {
            var avalancheRunner = _container.GetInstance<IAvalancheRunner>();
        }
    }
}