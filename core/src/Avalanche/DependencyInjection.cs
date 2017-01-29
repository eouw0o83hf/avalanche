using System;
using Avalanche.Glacier;
using Microsoft.Framework.DependencyInjection;
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
    public class DependencyInjectionInstaller
    {
        private readonly ExecutionParameters _executionParameters;
        
        public DependencyInjectionInstaller(ExecutionParameters executionParameters)
        {
            _executionParameters = executionParameters;
        }

        public void Install(IContainer container)
        {
            // Root/config dependencies
            container.Configure(_ =>
            {
                _.For<ExecutionParameters>().Use(_executionParameters);
                _.For(typeof(ILogger<>)).Use(typeof(Logger<>)).Singleton();
                _.For<ILoggerFactory>().Use<LoggerFactory>().Singleton();
            });

            var loggerFactory = container.GetInstance<ILoggerFactory>();
            loggerFactory.AddConsole();
            container.Release(loggerFactory);
            
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

            // // Lightroom
            // container.Configure(_ => 
            //     serviceCollection
            //         .AddTransient<ILightroomReader, LightroomReader>()
            //         .AddTransient<IDbConnection>(a =>
            //         {
            //             return new SqliteConnection($"DataSource={_executionParameters.Avalanche.CatalogFilePath}");
            //         });
            // );

            // // Runner
            // container.Configure(_ => 
            //     serviceCollection
            //         .AddSingleton<IAvalancheRunner, AvalancheRunner>();
            // );

            // // State
            // container.Configure(_ => 
            //     serviceCollection
            //         .AddTransient<IAvalancheRepository, AvalancheRepository>()
            //         .AddTransient<IDbConnection>(a =>
            //         {
            //             return new SqliteConnection($"DataSource={_executionParameters.Avalanche.AvalancheFilePath}");
            //         });
            // );
        }

        // private IServiceProvider Finalize(IServiceCollection serviceCollection)
        // {
        //     var container = serviceCollection.BuildServiceProvider();
        //     var logFactory = container.GetService<ILoggerFactory>();
        //     logFactory.AddConsole();

        //     return container;
        // }
    }
}