using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche.Glacier
{
    public interface IConsolePercentUpdater
    {
        void UpdatePercentage(string file, int percent);
    }

    public class ConsolePercentUpdater : IConsolePercentUpdater
    {
        private readonly ConcurrentDictionary<string, int> Percents = new ConcurrentDictionary<string, int>();

        public void UpdatePercentage(string file, int percent)
        {

        }
    }
}