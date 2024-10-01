using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.DM.Config
{
    /// <summary>
    /// Defines the properties section of HdMdNxM boxes
    /// </summary>
    public class HdMdNxM4kEPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("inputNames")] public Dictionary<string, InputPropertiesConfig> InputNames { get; set; }
    }

    public class HdMdNxM4kEBridgeablePropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("inputNames")] public Dictionary<uint, string> InputNames { get; set; }

        [JsonProperty("outputNames")] public Dictionary<uint, string> OutputNames { get; set; }
    }
}