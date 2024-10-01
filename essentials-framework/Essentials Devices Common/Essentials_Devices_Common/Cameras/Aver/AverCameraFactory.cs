using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using ViscaCameraPlugin;

namespace AverCameraPlugin
{
    public class AverCameraFactory : EssentialsPluginDeviceFactory<AverCameraDevice>
    {
        public AverCameraFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "avercamera" };
        }

        // Builds and returns an instance of EssentialsPluginDeviceTemplate
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
            if (comms == null)
            {
                Debug.Console(2, "[{0}] VISCA Camera: failed to create comms for {1}", dc.Key, dc.Name);
                return null;
            }

            ViscaCameraConfig propertiesConfig = dc.Properties.ToObject<ViscaCameraConfig>();
            if (propertiesConfig == null)
            {
                Debug.Console(2, "[{0}] Aver Camera: failed to read properties config for {1}", dc.Key, dc.Name);
                return null;
            }

            EssentialsControlPropertiesConfig commConfig = CommFactory.GetControlPropertiesConfig(dc);

            return new AverCameraDevice(dc.Key, dc.Name, comms, propertiesConfig, commConfig);
        }
    }
}