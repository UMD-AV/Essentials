using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
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