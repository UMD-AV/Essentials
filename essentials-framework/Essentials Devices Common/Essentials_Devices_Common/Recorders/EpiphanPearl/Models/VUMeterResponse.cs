using System.Collections.Generic;
using Newtonsoft.Json;

namespace UmdEssentials.EpiphanPearl.Models
{
    public class VUMeterResponse
    {
        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("status")] public DeviceStatusDetail Status { get; set; }
    }

    public class AudioLevels
    {
        [JsonProperty("peak")] public List<double> Peak { get; set; }

        [JsonProperty("rms")] public List<double> Rms { get; set; }
    }

    public class AudioStatus
    {
        [JsonProperty("state")] public string State { get; set; }

        [JsonProperty("levels")] public AudioLevels Levels { get; set; }
    }

    public class DeviceStatusDetail
    {
        [JsonProperty("audio")] public AudioStatus Audio { get; set; }
    }
}