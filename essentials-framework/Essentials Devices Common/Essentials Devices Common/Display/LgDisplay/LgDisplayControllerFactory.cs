﻿using System.Collections.Generic;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace Epi.Display.Lg
{
    public class LgDisplayControllerFactory:EssentialsDeviceFactory<LgDisplayController>
    {
        public LgDisplayControllerFactory()
        {
            TypeNames = new List<string> {"lgDisplay", "lg"};
        }

        #region Overrides of EssentialsDeviceFactory<LgDisplayController>

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var comms = CommFactory.CreateCommForDevice(dc);

            if (comms == null) return null;

            var config = dc.Properties.ToObject<LgDisplayPropertiesConfig>();

            return config == null ? null : new LgDisplayController(dc.Key, dc.Name, config, comms);
        }

        #endregion
    }
}