using Avalanche.Runner;
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
