using System;
using System.Collections.Generic;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.GeneralIO;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Core.CrestronIO
{
    public class CenIoComController : CrestronGenericBaseDevice, IComPorts
    {
        private CenIoCom _device;

        public CenIoComController(string key, Func<DeviceConfig, CenIoCom> preActivationFunc, DeviceConfig config)
            : base(key, config.Name)
        {
            AddPreActivationAction(() =>
            {
                _device = preActivationFunc(config);

                RegisterCrestronGenericBase(_device);
            });
        }

        #region Implementation of IComPorts

        public CrestronCollection<ComPort> ComPorts
        {
            get { return _device.ComPorts; }
        }

        public int NumberOfComPorts
        {
            get { return _device.NumberOfComPorts; }
        }

        #endregion
    }

    public class CenIoComControllerFactory : EssentialsDeviceFactory<CenIoComController>
    {
        public CenIoComControllerFactory()
        {
            TypeNames = new List<string>() { "ceniocom102", "ceniocom202" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new CEN-IO-COM Device");

            return new CenIoComController(dc.Key, GetCenIoComDevice, dc);
        }

        private static CenIoCom GetCenIoComDevice(DeviceConfig dc)
        {
            EssentialsControlPropertiesConfig control = CommFactory.GetControlPropertiesConfig(dc);
            uint ipid = control.IpIdInt;

            if (dc.Type.Contains("202"))
            {
                return new CenIoCom202(ipid, Global.ControlSystem);
            }
            else
            {
                return new CenIoCom102(ipid, Global.ControlSystem);
            }
        }
    }
}