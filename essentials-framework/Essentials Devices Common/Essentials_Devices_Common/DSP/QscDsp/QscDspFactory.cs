using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace QscQsysDspPlugin
{
    public class QscDspFactory : EssentialsPluginDeviceFactory<QscDsp>
    {
        public QscDspFactory()
        {
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            TypeNames = new List<string>() { "qscdsp" };
        }

        /// <summary>
        /// Builds the device using the configuration object
        /// </summary>
        /// <param name="dc">DeviceConfig</param>
        /// <returns>Device instance</returns>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new device from type: {0}", dc.Type);

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
            if (comms == null)
            {
                Debug.Console(2, "[{0}] QSC DSP: failed to create comms for {1}", dc.Key, dc.Name);
                return null;
            }

            QscDspPropertiesConfig propertiesConfig = dc.Properties.ToObject<QscDspPropertiesConfig>();
            if (propertiesConfig == null)
            {
                Debug.Console(2, "[{0}] QSC DSP: failed to read properties config for {1}", dc.Key, dc.Name);
                return null;
            }

            return new QscDsp(dc.Key, dc.Name, comms, dc);
            ;
        }
    }
}