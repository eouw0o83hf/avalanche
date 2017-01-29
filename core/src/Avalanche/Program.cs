using System;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;

namespace Avalanche
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var filename = ExecutionParameterHelpers.ResolveConfigFileLocation(args);
            if(filename == null)
            {
                return;
            }

            var config = ExecutionParameterHelpers.LoadExecutionParameters(filename);

            var container = new ServiceCollection().AddLogging();
            container
                .AddInstance(config)
                .AddSingleton<AvalancheRunner>()
                .AddSingleton<IConsolePercentUpdater, ConsolePercentUpdater>()
                .AddSingleton<IGlacierGateway, GlacierGateway>()
                .AddSingleton<ILightroomReader, LightroomReader>()
                .AddSingleton<IAvalancheRepository, AvalancheRepository>();

            var serviceProvider = container.BuildServiceProvider();
            var logFactory = serviceProvider.GetService<ILoggerFactory>();
            logFactory.AddConsole();
            
            var runner = serviceProvider.GetService<AvalancheRunner>();
            runner.Run();
        }
    }
}
