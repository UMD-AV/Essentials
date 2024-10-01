using Newtonsoft.Json;


namespace PepperDash.Essentials.Core
{
    public class EssentialsRfGatewayConfig
    {
        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("gatewayType")] public string GatewayType { get; set; }
    }
}