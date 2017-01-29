using System;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using StructureMap;

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

            var container = new Container();
            var installer = new DependencyInjectionInstaller(config);
            installer.Install(container);

            var runner = container.GetInstance<IAvalancheRunner>();
            runner.Run().Wait();
            container.Release(runner);
        }
    }
}
