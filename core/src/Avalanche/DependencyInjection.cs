using Avalanche.Glacier;
using Microsoft.Framework.Logging;
using Microsoft.Data.Sqlite;
using System.Data;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;
using StructureMap;
using Amazon.Glacier;
using Amazon;

namespace Avalanche
{
    // I couldn't quickly find an accepted pattern for StructureMap
    // installers, so I cargo-culted over the way Castle Windsor does it
    public class DependencyInjectionInstaller
    {
        private readonly ExecutionParameters _executionParameters;
        
        public DependencyInjectionInstaller(ExecutionParameters executionParameters)
        {
            _executionParameters = executionParameters;
        }

        public void Install(IContainer container)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole();

            // Root/config dependencies
            container.Configure(_ =>
            {
                _.For<ExecutionParameters>().Use(_executionParameters);
                _.For(typeof(ILogger<>)).Use(typeof(Logger<>)).Singleton();
                _.For<ILoggerFactory>().Use(loggerFactory).Singleton();
            });
            
            // Glacier
            container.Configure(_ => 
            {
                _.For<IAmazonGlacier>().Use<AmazonGlacierClient>()
                        .Ctor<string>("awsAccessKeyId").Is(_executionParameters.Glacier.AccessKeyId)
                        .Ctor<string>("awsSecretAccessKey").Is(_executionParameters.Glacier.SecretAccessKey)
                        .Ctor<RegionEndpoint>("region").Is(_executionParameters.Glacier.GetRegion())
                        .SelectConstructor(() => new AmazonGlacierClient("", "", RegionEndpoint.APNortheast1));
                
                _.For<IConsolePercentUpdater>().Use<ConsolePercentUpdater>().Singleton();
                _.For<IArchiveProvider>().Use<ArchiveProvider>().Singleton();
                _.For<IGlacierGateway>().Use<GlacierGateway>().Singleton()
                        .Ctor<string>("accountId").Is(_executionParameters.Glacier.AccountId);
            });

            // Lightroom
            container.Configure(_ =>
            {
                var lightroomDbInstanceName = "lightroomDbInstanceName";
                
                _.For<ILightroomReader>().Use<LightroomReader>().Transient()
                        .Ctor<IDbConnection>().IsNamedInstance(lightroomDbInstanceName);
                _.For<IDbConnection>().Use<SqliteConnection>().Transient()
                        .Named("lightroomDbInstanceName")
                        .Ctor<string>("connectionString").Is($"DataSource={_executionParameters.Avalanche.CatalogFilePath}")
                        .OnCreation(a => a.Open());
            });            

            // State
            container.Configure(_ =>
            {
                var avalancheDbInstanceName = "avalancheDbInstanceName";
                
                _.For<IAvalancheRepository>().Use<AvalancheRepository>().Transient()
                        .Ctor<IDbConnection>().IsNamedInstance(avalancheDbInstanceName);
                _.For<IDbConnection>().Use<SqliteConnection>().Transient()
                        .Named(avalancheDbInstanceName)
                        .Ctor<string>("connectionString").Is($"DataSource={_executionParameters.Avalanche.AvalancheFilePath}")
                        .OnCreation(a => a.Open());
            });

            // Runner
            container.Configure(_ =>
            {
                _.For<IAvalancheRunner>().Use<AvalancheRunner>().Singleton();
            });
        }
    }
}