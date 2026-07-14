using Newtonsoft.Json;

namespace UmdEssentials.EpiphanPearl
{
    public class EpiphanPearlControllerConfiguration
    {
        [JsonProperty("host")] public string Host { get; set; }

        [JsonProperty("secure")] public bool Secure { get; set; }

        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("password")] public string Password { get; set; }

        [JsonProperty("panoptoKey")] public string PanoptoKey { get; set; }

        [JsonProperty("contentChannel")] public string contentChannel { get; set; }
        [JsonProperty("camera1Channel")] public string camera1Channel { get; set; }
        [JsonProperty("camera2Channel")] public string camera2Channel { get; set; }
    }
}