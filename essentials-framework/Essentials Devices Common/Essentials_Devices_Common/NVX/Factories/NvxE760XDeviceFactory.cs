using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Devices;
using NvxEpi.Features.Config;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace NvxEpi.Factories
{
    public class NvxE760XDeviceFactory : NvxBaseDeviceFactory<NvxE760X>
    {
        private static List<string> _typeNames;

        public NvxE760XDeviceFactory()
        {
            if (_typeNames == null)
            {
                _typeNames = new List<string>
                {
                    "dmnvxe760",
                    "dmnvxe760c"
                };
            }

            TypeNames = _typeNames.ToList();
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            NvxDeviceProperties props = NvxDeviceProperties.FromDeviceConfig(dc);
            Func<DmNvxBaseClass> deviceBuild = GetDeviceBuildAction(dc.Type, props);
            return new NvxE760X(dc, deviceBuild);
        }
    }
}