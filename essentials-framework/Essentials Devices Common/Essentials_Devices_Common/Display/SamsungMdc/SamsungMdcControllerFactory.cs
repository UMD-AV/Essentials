using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Devices.Displays
{
    public class SamsungMdcControllerFactory : EssentialsDeviceFactory<SamsungMdcDisplayController>
    {
        public SamsungMdcControllerFactory() : base()
        {
            TypeNames = new List<string> { "samsungmdc" };
        }

        #region Overrides of EssentialsDeviceFactory<SamsungMdcDisplayController>

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);

            if (comms == null)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Unable to create comms for device {0}", dc.Key);
                return null;
            }

            SamsungMDCDisplayPropertiesConfig config = dc.Properties.ToObject<SamsungMDCDisplayPropertiesConfig>();

            if (config != null)
            {
                return new SamsungMdcDisplayController(dc.Key, dc.Name, config, comms);
            }

            Debug.Console(0, Debug.ErrorLogLevel.Error, "Unable to deserialize config for device {0}", dc.Key);
            return null;
        }

        #endregion
    }
}