﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlFactory: EssentialsDeviceFactory<EpiphanPearlController>
    {
        public EpiphanPearlFactory()
        {
            TypeNames = new List<string> { "epiphan" };
        }

        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            return new EpiphanPearlController(dc);
        }
    }
}