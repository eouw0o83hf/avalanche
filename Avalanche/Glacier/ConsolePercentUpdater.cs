using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche.Glacier
{
    public class ConsolePercentUpdater : IDisposable
    {
        private bool _run = false;
        private object _locker = new object();

        public int PercentDone { get; set; }

        public void Start()
        {
            _run = true;
            PercentDone = 0;

            lock (_locker)
            {
                Task.Run(() =>
                    {
                        var lastWrittenLength = 0;
                        while (_run)
                        {
                            for (var i = 0; i < lastWrittenLength; ++i)
                            {
                                Console.Write("\b");
                            }
                            var output = string.Format("{0}%", PercentDone);
                            lastWrittenLength = output.Length;
                            Console.Write(output);

                            Task.Delay(10).Wait();
                        }
                    });
            }
        }

        public void Stop()
        {
            _run = false;
            Task.Delay(10).Wait();
            Console.WriteLine();
        }

        public void Dispose()
        {
            if (_run)
            {
                Stop();
            }
        }
    }
}
