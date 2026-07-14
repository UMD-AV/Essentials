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
    public class Nvx38XDeviceFactory : NvxBaseDeviceFactory<Nvx38X>
    {
        private static IEnumerable<string> _typeNames;

        public Nvx38XDeviceFactory()
        {
            if (_typeNames == null)
                _typeNames = new List<string>
                {
                    "dmnvx384",
                    "dmnvx384c",
                    "dmnvx385",
                    "dmnvx385c"
                };

            TypeNames = _typeNames.ToList();
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            NvxDeviceProperties props = NvxDeviceProperties.FromDeviceConfig(dc);
            Func<DmNvxBaseClass> deviceBuild = GetDeviceBuildAction(dc.Type, props);
            return new Nvx38X(dc, deviceBuild, props.DeviceIsTransmitter());
        }
    }
}