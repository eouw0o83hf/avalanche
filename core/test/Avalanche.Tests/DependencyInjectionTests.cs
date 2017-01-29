using System;
using System.Linq;
using Xunit;
using StructureMap;
using Avalanche.Glacier;
using Microsoft.Framework.Logging;
using Amazon;
using Amazon.Glacier;

namespace Avalanche.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void DiSeemsToWork()
        {
            var container = new Container();
            var config = new ExecutionParameters
            {
                Avalanche = new AvalancheParameters
                {

                },
                Glacier = new GlacierParameters
                {
                    AccountId = "test account id",
                    AccessKeyId = "key id",
                    SecretAccessKey = "such secret very wow",
                    Region = RegionEndpoint.USEast1.SystemName
                }
            };
            new DependencyInjectionInstaller(config).Install(container);
            var thing = container.GetInstance<ILogger<DependencyInjectionTests>>();
            thing.LogInformation("OH HAI THERE");
            var thing3 = container.GetInstance<IAmazonGlacier>();
            var thing2 = container.GetInstance<IGlacierGateway>();
        }
    }
}