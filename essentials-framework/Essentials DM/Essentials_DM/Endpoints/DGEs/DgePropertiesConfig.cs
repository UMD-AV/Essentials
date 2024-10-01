using PepperDash.Core;
using Newtonsoft.Json;

namespace PepperDash.Essentials.DM.Endpoints.DGEs
{
    public class DgePropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }
}