using Newtonsoft.Json;

namespace PepperDash.Essentials.Core
{
    public class CrestronRemotePropertiesConfig
    {
        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("gatewayDeviceKey")] public string GatewayDeviceKey { get; set; }
    }
}