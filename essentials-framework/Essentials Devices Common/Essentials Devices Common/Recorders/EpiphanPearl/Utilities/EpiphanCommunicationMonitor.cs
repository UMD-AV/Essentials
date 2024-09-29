using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.EpiphanPearl.Utilities
{
    public class EpiphanCommunicationMonitor:StatusMonitorBase
    {
        private bool _isStarted;

        public EpiphanCommunicationMonitor(IKeyed parent, long warningTime, long errorTime) : base(parent, warningTime, errorTime)
        {
        }

        public override void Start()
        {
            _isStarted = true;
            UpdateTimers();
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
            }

            UpdateTimers();
        }

        public void UpdateTimers()
        {
            if (!_isStarted)
                return;

            if (Status == MonitorStatus.IsOk)
            {
                StopErrorTimers();
                return;
            }
            StartErrorTimers();
            
        }
    }
}