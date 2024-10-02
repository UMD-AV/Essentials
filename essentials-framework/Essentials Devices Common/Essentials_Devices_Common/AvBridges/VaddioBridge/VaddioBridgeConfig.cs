using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace VaddioBridgePlugin
{
    public class VaddioBridgeConfig
    {
        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("username")] public string Username { get; set; }

        [JsonProperty("password")] public string Password { get; set; }

        [JsonProperty("pollTimeMs")] public long PollTimeMs { get; set; }

        [JsonProperty("warningTimeoutMs")] public long WarningTimeoutMs { get; set; }

        [JsonProperty("errorTimeoutMs")] public long ErrorTimeoutMs { get; set; }
    }
}