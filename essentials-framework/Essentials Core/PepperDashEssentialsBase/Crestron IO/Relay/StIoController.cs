using System.Collections.Generic;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.GeneralIO;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Core
{
    /// <summary>
    /// Wrapper class for ST-IO relay module
    /// </summary>
    [Description("Wrapper class for the ST-IO relay module")]
    public class StIoController : CrestronGenericBaseDevice, IRelayPorts
    {
        private readonly StIo _stIo;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="ry104"></param>
        public StIoController(string key, string name, StIo stIo)
            : base(key, name, stIo)
        {
            _stIo = stIo;
        }

        /// <summary>
        /// Relay port collection
        /// </summary>
        public CrestronCollection<Relay> RelayPorts
        {
            get { return _stIo.RelayPorts; }
        }

        /// <summary>
        /// Number of relay ports property
        /// </summary>
        public int NumberOfRelayPorts
        {
            get { return _stIo.NumberOfRelayPorts; }
        }
    }

    /// <summary>
    /// ST-IO Controller factory
    /// </summary>
    public class StIoControllerFactory : EssentialsDeviceFactory<StIoController>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public StIoControllerFactory()
        {
            TypeNames = new List<string>() { "stio" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create a new ST-IO Device");

            EssentialsControlPropertiesConfig controlPropertiesConfig = CommFactory.GetControlPropertiesConfig(dc);
            if (controlPropertiesConfig == null)
            {
                Debug.Console(1, "Factory failed to create a new ST-IO Device, control properties not found");
                return null;
            }

            uint id = controlPropertiesConfig.CresnetIdInt;
            if (id != 0) return new StIoController(dc.Key, dc.Name, new StIo(id, Global.ControlSystem));

            Debug.Console(1, "Factory failed to create a ST-IO Device using cresnet ID {0}", id);
            return null;
        }
    }
}