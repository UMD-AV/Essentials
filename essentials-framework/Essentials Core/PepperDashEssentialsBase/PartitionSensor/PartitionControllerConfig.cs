using Newtonsoft.Json;

namespace UmdEssentials.Core.PartitionSensor
{
    public class PartitionControllerConfig
    {
        [JsonProperty("state")] public string State { get; set; }

        [JsonProperty("divided")] public RoomState Divided { get; set; }

        [JsonProperty("combined")] public RoomState Combined { get; set; }
    }

    public class RoomState
    {
        [JsonProperty("tp01RoomKey")] public string Tp01RoomKey { get; set; }
        [JsonProperty("tp02RoomKey")] public string Tp02RoomKey { get; set; }
        [JsonProperty("tp03RoomKey")] public string Tp03RoomKey { get; set; }
        [JsonProperty("tp04RoomKey")] public string Tp04RoomKey { get; set; }
        [JsonProperty("preset")] public ushort Preset { get; set; }
    }
}