using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlControllerConfiguration
    {
        [JsonProperty("host")] public string Host { get; set; }

        [JsonProperty("secure")] public bool Secure { get; set; }

        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("password")] public string Password { get; set; }

        [JsonProperty("panoptoKey")] public string PanoptoKey { get; set; }

        [JsonProperty("stream1url")] public string Stream1Url { get; set; }
        [JsonProperty("stream2url")] public string Stream2Url { get; set; }
        [JsonProperty("stream3url")] public string Stream3Url { get; set; }
    }
}