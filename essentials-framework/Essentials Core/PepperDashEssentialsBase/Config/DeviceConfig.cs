using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UmdEssentials.Core.Config
{
    public class DeviceConfig
    {
        [JsonProperty("key")] public string Key { get; set; }

        [JsonProperty("uid")] public int Uid { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("group")] public string Group { get; set; }

        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("properties")] public JToken Properties { get; set; }

        public DeviceConfig(DeviceConfig dc)
        {
            Key = dc.Key;
            Uid = dc.Uid;
            Name = dc.Name;
            Group = dc.Group;
            Type = dc.Type;

            Properties = JToken.Parse(dc.Properties.ToString());
        }

        public DeviceConfig()
        {
        }
    }
}