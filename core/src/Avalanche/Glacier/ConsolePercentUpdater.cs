using System;
using System.Collections.Concurrent;

namespace Avalanche.Glacier
{
    public interface IConsolePercentUpdater
    {
        void UpdatePercentage(string file, int percent);
    }

    // I'm not terribly sure how I want this component to work, but
    // this interface gives a foundation to work with when everything
    // else is wired together and mostly working.
    public class ConsolePercentUpdater : IConsolePercentUpdater
    {
        public void UpdatePercentage(string file, int percent)
        {
            Console.WriteLine($"{file} is at {percent}%");
        }
    }
}