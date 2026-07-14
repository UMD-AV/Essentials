using Newtonsoft.Json;


namespace UmdEssentials.Core
{
    public class EssentialsRfGatewayConfig
    {
        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("gatewayType")] public string GatewayType { get; set; }
    }
}