using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.LightwareUcx
{
    public class LightwareUcxPropertiesConfig
    {
        [JsonProperty("videoinputNames")] public Dictionary<uint, string> VideoInputNames { get; set; }
        [JsonProperty("audioinputNames")] public Dictionary<uint, string> AudioInputNames { get; set; }
        [JsonProperty("usbinputNames")] public Dictionary<uint, string> UsbInputNames { get; set; }
        [JsonProperty("videoOutputNames")] public Dictionary<uint, string> VideoOutputNames { get; set; }
        [JsonProperty("audioOutputNames")] public Dictionary<uint, string> AudioOutputNames { get; set; }

        [JsonProperty("outputMonitoringEnabled")]
        public Dictionary<uint, bool> OutputMonitoringEnabled { get; set; }

        [JsonProperty("config")] public IList<string> Config { get; set; }
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }
}