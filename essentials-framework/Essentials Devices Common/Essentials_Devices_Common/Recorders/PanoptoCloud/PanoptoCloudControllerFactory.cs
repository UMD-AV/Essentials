using System.Collections.Generic;
using UmdEssentials.Core;
using UmdEssentials.Core.Config;

namespace UmdEssentials.PanoptoCloud
{
    public class PanoptoCloudControllerFactory : EssentialsDeviceFactory<PanoptoCloudController>
    {
        public PanoptoCloudControllerFactory()
        {
            TypeNames = new List<string> { "panoptocloud" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            return new PanoptoCloudController(dc);
        }
    }
}