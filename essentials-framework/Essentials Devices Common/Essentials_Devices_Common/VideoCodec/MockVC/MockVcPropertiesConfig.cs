using System.Collections.Generic;
using Newtonsoft.Json;
using UmdEssentials.Devices.Common.Codec;

namespace UmdEssentials.Devices.Common.VideoCodec
{
    public class MockVcPropertiesConfig
    {
        [JsonProperty("favorites")] public List<CodecActiveCallItem> Favorites { get; set; }

        [JsonProperty("presets")] public List<CodecRoomPreset> Presets { get; set; }

        public MockVcPropertiesConfig()
        {
            Favorites = new List<CodecActiveCallItem>();
            Presets = new List<CodecRoomPreset>();
        }
    }
}