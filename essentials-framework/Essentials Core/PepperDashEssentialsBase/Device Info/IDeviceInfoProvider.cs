using PepperDash.Core;

namespace UmdEssentials.Core.DeviceInfo
{
    public interface IDeviceInfoProvider : IKeyed
    {
        DeviceInfo DeviceInfo { get; }

        event DeviceInfoChangeHandler DeviceInfoChanged;

        void UpdateDeviceInfo();
    }

    public delegate void DeviceInfoChangeHandler(IKeyed device, DeviceInfoEventArgs args);
}