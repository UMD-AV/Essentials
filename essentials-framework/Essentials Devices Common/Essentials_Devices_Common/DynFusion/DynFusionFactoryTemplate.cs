using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace DynFusion
{
    public class EssentialsFactoryTemplate : EssentialsDeviceFactory<EssentialsBridgeableDevice>
    {
        public EssentialsFactoryTemplate()
        {
            // In the constructor we initialize the list with the type names that will build an instance of this device
            TypeNames = new List<string>() { "DynFusion", "DynFusionSchedule" };
        }

        // Builds and returns an instance of EssentialsPluginDeviceTemplate
        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);


            switch (dc.Type)
            {
                case "DynFusion":
                {
                    DynFusionConfigObjectTemplate propertiesConfig =
                        dc.Properties.ToObject<DynFusionConfigObjectTemplate>();
                    return new DynFusionDevice(dc.Key, dc.Name, propertiesConfig);
                }
                case "DynFusionSchedule":
                {
                    SchedulingConfig propertiesConfig = dc.Properties.ToObject<SchedulingConfig>();
                    return new DynFusionSchedule(dc.Key, dc.Name, propertiesConfig);
                }
                default:
                    return null;
            }
        }
    }
}