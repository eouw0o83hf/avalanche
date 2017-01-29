using System;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.Repository;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;

namespace Avalanche
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var container = new ServiceCollection().AddLogging();
            container
                .AddSingleton<AvalancheRunner>()
                .AddSingleton<IConsolePercentUpdater, ConsolePercentUpdater>()
                .AddSingleton<GlacierGateway>()
                .AddSingleton<ILightroomReader, LightroomReader>()
                .AddSingleton<AvalancheRepository>();

            var serviceProvider = container.BuildServiceProvider();
            var logFactory = serviceProvider.GetService<ILoggerFactory>();
            logFactory.AddConsole();
            
            var runner = serviceProvider.GetService<AvalancheRunner>();
            runner.Run();
        }
    }

    public class AvalancheRunner
    {
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly string[] _args;
        
        public AvalancheRunner(ILogger<AvalancheRunner> logger, string[] args)
        {
            _logger = logger;
            _args = args;
        }

        public void Run()
        {
            var filename = ExecutionParameterHelpers.ResolveConfigFileLocation(_args);
            if(filename == null)
            {
                return;
            }

            var config = ExecutionParameterHelpers.LoadExecutionParameters(filename);
        }
    }
}
