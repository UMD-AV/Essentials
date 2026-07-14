using Crestron.SimplSharpPro.DeviceSupport;
using UmdEssentials.Core.Bridges;

namespace UmdEssentials.Core
{
    public abstract class EssentialsBridgeableDevice : EssentialsDevice, IBridgeAdvanced
    {
        protected EssentialsBridgeableDevice(string key) : base(key)
        {
        }

        protected EssentialsBridgeableDevice(string key, string name) : base(key, name)
        {
        }

        public abstract void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge);
    }
}