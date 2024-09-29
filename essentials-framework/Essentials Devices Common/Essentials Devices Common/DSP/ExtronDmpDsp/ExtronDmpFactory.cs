using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;

namespace ExtronDmp
{
    public class ExtronDmpFactory : EssentialsDeviceFactory<ExtronDmp>
    {
        public ExtronDmpFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "extrondmp" };
        }

        // Builds and returns an instance of EssentialsPluginDeviceTemplate
        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);			

	        var comms = CommFactory.CreateCommForDevice(dc);
	        if (comms == null)
	        {
		        Debug.Console(2, "[{0}] Extron Dmp: failed to create comms for {1}", dc.Key, dc.Name);
		        return null;
	        }
            
            var propertiesConfig = dc.Properties.ToObject<ExtronDmpConfig>();
	        if (propertiesConfig == null)
	        {
                Debug.Console(2, "[{0}] Extron Dmp: failed to read properties config for {1}", dc.Key, dc.Name);
		        return null;
	        }

            return new ExtronDmp(dc.Key, dc.Name, comms, propertiesConfig);
        }
    }
}