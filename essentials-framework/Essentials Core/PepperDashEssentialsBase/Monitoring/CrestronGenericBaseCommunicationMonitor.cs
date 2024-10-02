﻿using Crestron.SimplSharpPro;
using PepperDash.Core;


namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class CrestronGenericBaseCommunicationMonitor : StatusMonitorBase
    {
        private GenericBase Device;

        public CrestronGenericBaseCommunicationMonitor(IKeyed parent, GenericBase device, long warningTime,
            long errorTime)
            : base(parent, warningTime, errorTime)
        {
            Device = device;
        }

        public override void Start()
        {
            Device.OnlineStatusChange -= Device_OnlineStatusChange;
            Device.OnlineStatusChange += Device_OnlineStatusChange;
            GetStatus();
        }

        public override void Stop()
        {
            Device.OnlineStatusChange -= Device_OnlineStatusChange;
        }

        private void Device_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            GetStatus();
        }

        private void GetStatus()
        {
            if (Device.IsOnline)
            {
                Status = MonitorStatus.IsOk;
                StopErrorTimers();
            }
            else
                StartErrorTimers();
        }
    }
}