using System.Collections.Generic;
using UmdEssentials.Core;

namespace UmdEssentials.EpiphanPearl
{
    public class EpiphanPearlFactory : EssentialsDeviceFactory<EpiphanPearlController>
    {
        public EpiphanPearlFactory()
        {
            TypeNames = new List<string> { "epiphan" };
        }

        public override EssentialsDevice BuildDevice(Core.Config.DeviceConfig dc)
        {
            return new EpiphanPearlController(dc);
        }
    }
}