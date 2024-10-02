using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Core.Config
{
    /// <summary>
    /// Loads the ConfigObject from the file
    /// </summary>
    public class EssentialsConfig : BasicConfig
    {
        [JsonProperty("systemUuid")] public string SystemUuid { get; set; }

        [JsonProperty("rooms")] public List<DeviceConfig> Rooms { get; set; }

        public EssentialsConfig()
        {
            Rooms = new List<DeviceConfig>();
        }
    }
}