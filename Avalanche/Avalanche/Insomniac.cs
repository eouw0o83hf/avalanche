using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche
{
    public class Insomniac : IDisposable
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));
        private readonly uint _previousState;

        public Insomniac()
        {
            _previousState = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);

            if (_previousState == 0)
            {
                _log.Warn("Couldn't set thread state; the application may be unable to prevent the computer's going to sleep.");
            }
        }

        public void Dispose()
        {
            SetThreadExecutionState(_previousState);
        }

        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
    }
}
