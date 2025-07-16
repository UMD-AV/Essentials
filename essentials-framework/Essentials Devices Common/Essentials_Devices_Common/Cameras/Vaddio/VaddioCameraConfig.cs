using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace VaddioCameraPlugin
{
    public class VaddioCameraConfig
    {
        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("address")] public uint Address { get; set; }

        [JsonProperty("panSpeed")] public uint? PanSpeed { get; set; }

        [JsonProperty("tiltSpeed")] public uint? TiltSpeed { get; set; }

        [JsonProperty("zoomSpeed")] public uint? ZoomSpeed { get; set; }

        [JsonProperty("focusSpeed")] public uint? FocusSpeed { get; set; }

        [JsonProperty("privacyOnPreset")] public uint? PrivacyOnPreset { get; set; }

        [JsonProperty("privacyOffPreset")] public uint? PrivacyOffPreset { get; set; }


        [JsonProperty("homePreset")] public uint? HomePreset { get; set; }

        [JsonProperty("presets")] public List<VaddioCameraPresetConfig> Presets { get; set; }
    }

    public class VaddioCameraPresetConfig
    {
        [JsonProperty("index")] public uint Index { get; set; }

        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("viscaId")] public uint? ViscaId { get; set; }
    }
}