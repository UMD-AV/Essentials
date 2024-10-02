using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Devices.Displays
{
    public class SamsungMDCDisplayPropertiesConfig
    {
        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("volumeUpperLimit")] public int? volumeUpperLimit { get; set; }

        [JsonProperty("volumeLowerLimit")] public int? volumeLowerLimit { get; set; }

        [JsonProperty("defaultVolume")] public int? defaultVolume { get; set; }

        [JsonProperty("pollIntervalMs")] public long pollIntervalMs { get; set; }

        [JsonProperty("coolingTimeMs")] public uint coolingTimeMs { get; set; }

        [JsonProperty("warmingTimeMs")] public uint warmingTimeMs { get; set; }

        [JsonProperty("showVolumeControls")] public bool showVolumeControls { get; set; }

        [JsonProperty("friendlyNames")] public List<FriendlyName> FriendlyNames { get; set; }

        [JsonProperty("videoMuteKey")] public string VideoMuteKey { get; set; }

        [JsonProperty("videoMuteInput")] public int VideoMuteInput { get; set; }

        public SamsungMDCDisplayPropertiesConfig()
        {
            FriendlyNames = new List<FriendlyName>();
        }
    }

    public class FriendlyName
    {
        [JsonProperty("inputKey")] public string InputKey { get; set; }

        [JsonProperty("name")] public string Name { get; set; }
    }
}