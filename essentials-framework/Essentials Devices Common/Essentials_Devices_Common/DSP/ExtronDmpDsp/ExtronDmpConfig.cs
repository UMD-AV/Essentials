using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;


namespace ExtronDmp
{
    /// <summary>
    /// Converge Pro DSP Properties config class
    /// </summary>
    public class ExtronDmpConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        [JsonProperty("control")] public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("deviceId")] public string DeviceId { get; set; }

        [JsonProperty("levelControlBlocks")]
        public Dictionary<string, ExtronDmpControlBlockConfig> LevelControlBlocks { get; set; }

        [JsonProperty("presets")] public Dictionary<string, ExtronDmpPreset> Presets { get; set; }

        [JsonProperty("dialerControlBlocks")]
        public Dictionary<string, ExtronDmpDialerConfig> DialerControlBlocks { get; set; }
    }

    /// <summary>
    /// Converge Pro Presets Configurations
    /// </summary>
    public class ExtronDmpPreset
    {
        [JsonProperty("label")] public string Label { get; set; }

        [JsonProperty("id")] public ushort id { get; set; }

        [JsonProperty("isMacro")] public bool isMacro { get; set; }
    }

    public class ExtronDmpDialerConfig
    {
        [JsonProperty("label")] public string Label { get; set; }

        [JsonProperty("LineNumber")] public ushort LineNumber { get; set; }
    }

    /// <summary>
    /// Converge Pro Level Control Block Configuration 
    /// </summary>
    public class ExtronDmpControlBlockConfig
    {
        [JsonProperty("label")] public string Label { get; set; }

        [JsonProperty("controlId")] public int? ControlId { get; set; }

        [JsonProperty("levelGroup")] public int? LevelGroup { get; set; }

        [JsonProperty("muteGroup")] public int? MuteGroup { get; set; }

        [JsonProperty("disabled")] public bool? Disabled { get; set; }

        [JsonProperty("isMic")] public bool? IsMic { get; set; }

        [JsonProperty("min")] public int? Min { get; set; }

        [JsonProperty("max")] public int? Max { get; set; }
    }
}