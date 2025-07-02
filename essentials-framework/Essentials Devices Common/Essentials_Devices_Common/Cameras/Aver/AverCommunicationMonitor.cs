using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.AverCamera
{
    public class AverCommunicationMonitor : StatusMonitorBase
    {
        private bool _isStarted;

        public AverCommunicationMonitor(IKeyed parent, long warningTime, long errorTime) : base(parent, warningTime,
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