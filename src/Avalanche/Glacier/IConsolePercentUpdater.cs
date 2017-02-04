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
        // Making some tradeoffs to this so I can run it and get up to date.
        // Long-term, it needs to handle multi-threaded updating
        private int _lastUpdateAmount = -1;
        
        // todo: make this not stupid
        public void UpdatePercentage(string file, int percent)
        {
            // Only update every 10% to curb the spam
            percent = (percent / 10) * 10;
            if(percent == _lastUpdateAmount)
            {
                return;
            }
           
            Console.WriteLine($"{file} is at {percent}%");
            _lastUpdateAmount = percent;
        }
    }
}