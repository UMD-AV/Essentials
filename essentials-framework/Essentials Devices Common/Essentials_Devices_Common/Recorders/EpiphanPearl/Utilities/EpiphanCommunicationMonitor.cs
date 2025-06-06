using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.EpiphanPearl.Utilities
{
    public class EpiphanCommunicationMonitor : StatusMonitorBase
    {
        private bool _isStarted;

        public EpiphanCommunicationMonitor(IKeyed parent, long warningTime, long errorTime) : base(parent, warningTime,
            errorTime)
        {
        }

        public override void Start()
        {
            _isStarted = true;
            StartErrorTimers();
        }

        public override void Stop()
        {
            _isStarted = false;
            StopErrorTimers();
        }

        public void SetOnlineStatus(bool isOnline)
        {
            if (isOnline)
            {
                Status = MonitorStatus.IsOk;
                StopErrorTimers();
                return;
            }

            if (!_isStarted)
                return;

            StartErrorTimers();
        }
    }
}