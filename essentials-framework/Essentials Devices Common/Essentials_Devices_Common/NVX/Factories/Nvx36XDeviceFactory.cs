using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Devices;
using NvxEpi.Features.Config;
using UmdEssentials.Core;
using UmdEssentials.Core.Config;

namespace NvxEpi.Factories
{
    public class Nvx36XDeviceFactory : NvxBaseDeviceFactory<Nvx36X>
    {
        private static IEnumerable<string> _typeNames;

        public Nvx36XDeviceFactory()
        {
            if (_typeNames == null)
                _typeNames = new List<string>
                {
                    "dmnvx360",
                    "dmnvx360c",
                    "dmnvx363",
                    "dmnvx363c"
                };

            TypeNames = _typeNames.ToList();
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            NvxDeviceProperties props = NvxDeviceProperties.FromDeviceConfig(dc);
            Func<DmNvxBaseClass> deviceBuild = GetDeviceBuildAction(dc.Type, props);
            return new Nvx36X(dc, deviceBuild, props.DeviceIsTransmitter());
        }
    }
}