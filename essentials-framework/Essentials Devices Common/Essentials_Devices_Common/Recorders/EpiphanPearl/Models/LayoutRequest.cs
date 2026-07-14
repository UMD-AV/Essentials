using System;
using Newtonsoft.Json;

namespace UmdEssentials.EpiphanPearl.Models
{
    public class LayoutRequest
    {
        [JsonProperty("id")] public string id { get; set; }
    }
}