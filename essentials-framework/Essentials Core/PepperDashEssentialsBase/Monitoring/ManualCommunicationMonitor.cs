using PepperDash.Core;
using UmdEssentials.Core;

namespace PepperDash_Essentials_Core.Monitoring
{
    public class ManualCommunicationMonitor : StatusMonitorBase
    {
        private bool _isStarted;

        public ManualCommunicationMonitor(IKeyed parent, long warningTime, long errorTime) : base(parent, warningTime,
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
                ResetErrorTimers();
                return;
            }

            if (!_isStarted)
                return;

            StartErrorTimers();
        }
    }
}