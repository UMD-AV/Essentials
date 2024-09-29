using System;
using PepperDash.Essentials.Core.Config;


namespace PepperDash.Essentials.Core.Devices
{
    public interface IReconfigurableDevice
    {
        event EventHandler<EventArgs> ConfigChanged;

        DeviceConfig Config { get; }

        void SetConfig(DeviceConfig config);
    }
}