using System;
using UmdEssentials.Core.Config;


namespace UmdEssentials.Core.Devices
{
    public interface IReconfigurableDevice
    {
        event EventHandler<EventArgs> ConfigChanged;

        DeviceConfig Config { get; }

        void SetConfig(DeviceConfig config);
    }
}