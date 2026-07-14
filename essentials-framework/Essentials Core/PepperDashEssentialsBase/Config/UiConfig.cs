using System.Collections.Generic;
using Newtonsoft.Json;

namespace UmdEssentials.Core.Config
{
    public class UiConfig
    {
        [JsonProperty("key")] public string Key { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("defaultRoomKey")] public string DefaultRoomKey { get; set; }

        [JsonProperty("techPanel")] public bool? TechPanel { get; set; }

        [JsonProperty("techPassword")] public string TechPassword { get; set; }

        [JsonProperty("userPassword")] public string UserPassword { get; set; }

        [JsonProperty("scheduleKey")] public string ScheduleKey { get; set; }

        [JsonProperty("previewRoutes")] public List<Route> PreviewRoutes { get; set; }
    }
}