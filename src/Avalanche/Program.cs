using System;
using Avalanche.Runner;
using StructureMap;

this is not valid c#.

namespace Avalanche
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var commandConfig = ExecutionParameterHelpers.ResolveConfigFileLocation(args);
            if(commandConfig == null)
            {
                return;
            }
            
            var config = ExecutionParameterHelpers.LoadExecutionParameters(commandConfig);


            // Make announcements before we launch everything
            if(commandConfig.TestMode)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Test mode was flagged, so nothing will be persisted.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("This thing is armed and dangerous, about to send files. Hit enter to continue.");
                Console.ReadLine();
            }
            Console.ResetColor();


            using(var container = new Container())
            {
                var installer = new DependencyInjectionInstaller(config);
                installer.Install(container);

                var runner = container.GetInstance<IAvalancheRunner>();
                runner.Run().GetAwaiter().GetResult();
                container.Release(runner);
            }
        }
    }
}
