using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.PanoptoCloud
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