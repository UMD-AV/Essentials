using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.Video.LightwareUcx
{
    public class LightwareUcxPropertiesConfig
    {
        [JsonProperty("videoInputNames")] public Dictionary<uint, string> VideoInputNames { get; set; }
        [JsonProperty("audioInputNames")] public Dictionary<uint, string> AudioInputNames { get; set; }
        [JsonProperty("usbInputNames")] public Dictionary<uint, string> UsbInputNames { get; set; }
        [JsonProperty("videoOutputNames")] public Dictionary<uint, string> VideoOutputNames { get; set; }
        [JsonProperty("audioOutputNames")] public Dictionary<uint, string> AudioOutputNames { get; set; }

        [JsonProperty("outputMonitoringEnabled")]
        public Dictionary<uint, bool> OutputMonitoringEnabled { get; set; }

        [JsonProperty("nightlyRebootEnabled")] public bool? NightlyRebootEnabled { get; set; }
        [JsonProperty("config")] public IList<string> Config { get; set; }
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }
}