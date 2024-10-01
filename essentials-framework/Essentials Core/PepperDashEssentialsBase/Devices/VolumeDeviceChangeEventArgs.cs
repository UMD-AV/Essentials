﻿using System;

namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class VolumeDeviceChangeEventArgs : EventArgs
    {
        public IBasicVolumeControls OldDev { get; private set; }
        public IBasicVolumeControls NewDev { get; private set; }
        public ChangeType Type { get; private set; }

        public VolumeDeviceChangeEventArgs(IBasicVolumeControls oldDev, IBasicVolumeControls newDev, ChangeType type)
        {
            OldDev = oldDev;
            NewDev = newDev;
            Type = type;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public enum ChangeType
    {
        WillChange,
        DidChange
    }
}