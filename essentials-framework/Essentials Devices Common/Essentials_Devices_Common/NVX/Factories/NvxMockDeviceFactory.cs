using System.Collections.Generic;
using NvxEpi.Devices;
using UmdEssentials.Core;
using UmdEssentials.Core.Config;

namespace NvxEpi.Factories
{
    public class NvxMockDeviceFactory : NvxBaseDeviceFactory<NvxMockDevice>
    {
        public NvxMockDeviceFactory()
        {
            TypeNames = new List<string> { "mocknvxdevice" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            return new NvxMockDevice(dc);
        }
    }
}