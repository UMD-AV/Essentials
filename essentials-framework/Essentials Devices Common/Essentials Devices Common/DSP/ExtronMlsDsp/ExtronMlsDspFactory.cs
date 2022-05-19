using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace ExtronMlsDspPlugin
{
    public class ExtronMlsDspFactory : EssentialsPluginDeviceFactory<ExtronMlsDsp>
    {
        public ExtronMlsDspFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "extronmls" };
        }

        /// <summary>
        /// Builds the device using the configuration object
        /// </summary>
        /// <param name="dc">DeviceConfig</param>
        /// <returns>Device instance</returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);

            var comms = CommFactory.CreateCommForDevice(dc);
            if (comms == null)
            {
                Debug.Console(0, "[{0}] Extron MLS DSP: failed to create comms for {1}", dc.Key, dc.Name);
                return null;
            }

            return new ExtronMlsDsp(dc.Key, dc.Name, comms); ;
        }
    }
}