using PepperDash.Core;
using Newtonsoft.Json;

namespace UmdEssentials.DM.AirMedia
{
    public class AirMediaPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("autoSwitching")] public bool AutoSwitchingEnabled { get; set; }
    }
}