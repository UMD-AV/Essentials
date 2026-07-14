using PepperDash.Core;
using Newtonsoft.Json;

namespace UmdEssentials.DM.Endpoints.DGEs
{
    public class DgePropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }
}