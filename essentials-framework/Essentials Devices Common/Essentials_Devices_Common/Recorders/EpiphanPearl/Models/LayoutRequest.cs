using System;
using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl.Models
{
    public class LayoutRequest
    {
        [JsonProperty("id")] public string id { get; set; }
    }
}