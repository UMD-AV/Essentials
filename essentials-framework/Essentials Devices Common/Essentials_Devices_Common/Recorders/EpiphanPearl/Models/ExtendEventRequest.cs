using System;
using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl.Models
{
    public class ExtendEventRequest
    {
        [JsonProperty("finish")]
        [JsonConverter(typeof(SecondEpochConverter))]
        public DateTime Finish { get; set; }
    }
}