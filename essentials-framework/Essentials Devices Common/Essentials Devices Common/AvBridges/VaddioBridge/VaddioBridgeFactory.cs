﻿using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace VaddioBridgePlugin
{
    public class VaddioBridgeFactory : EssentialsPluginDeviceFactory<VaddioBridgeDevice>
    {
        public VaddioBridgeFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "vaddiobridge" };
        }

        // Builds and returns an instance of EssentialsPluginDeviceTemplate
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);			

	        var comms = CommFactory.CreateCommForDevice(dc);
	        if (comms == null)
	        {
		        Debug.Console(2, "[{0}] Vaddio Bridge: failed to create comms for {1}", dc.Key, dc.Name);
		        return null;
	        }
            
            var propertiesConfig = dc.Properties.ToObject<VaddioBridgeConfig>();
	        if (propertiesConfig == null)
	        {
		        Debug.Console(2, "[{0}] Vaddio Bridge: failed to read properties config for {1}", dc.Key, dc.Name);
		        return null;
	        }					

			return new VaddioBridgeDevice(dc.Key, dc.Name, comms, propertiesConfig);
        }

    }
}