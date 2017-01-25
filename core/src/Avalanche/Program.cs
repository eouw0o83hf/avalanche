using System;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;

namespace Avalanche
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var container = new ServiceCollection().AddLogging();
            container.AddSingleton<AvalancheRunner>();

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
            _logger.LogInformation("Hello World!");
        }
    }
}
