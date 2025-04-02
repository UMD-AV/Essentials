using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash_Essentials_DM.Config
{
    public class HdPsXxxPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("inputNames")] public Dictionary<uint, string> InputNames { get; set; }

        [JsonProperty("outputNames")] public Dictionary<uint, string> OutputNames { get; set; }

        // "inputPriorities": "1,4,3,2"
        [JsonProperty("inputPriorities")] public string InputPriorities { get; set; }

        public HdPsXxxPropertiesConfig()
        {
            InputNames = new Dictionary<uint, string>();
            OutputNames = new Dictionary<uint, string>();
        }
    }
}