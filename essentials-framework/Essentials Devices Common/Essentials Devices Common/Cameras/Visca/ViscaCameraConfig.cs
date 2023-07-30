using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace ViscaCameraPlugin
{
	public class ViscaCameraConfig
	{
		[JsonProperty("control")]
		public EssentialsControlPropertiesConfig Control { get; set; }

		[JsonProperty("deviceId")]
		public long DeviceId { get; set; }

		[JsonProperty("enabled")]
		public bool Enabled { get; set; }

		[JsonProperty("address")]
		public uint Address { get; set; }

		[JsonProperty("panSpeed")]
		public uint PanSpeed { get; set; }

		[JsonProperty("tiltSpeed")]
		public uint TiltSpeed { get; set; }

		[JsonProperty("zoomSpeed")]
		public uint ZoomSpeed { get; set; }

		[JsonProperty("focusSpeed")]
		public uint FocusSpeed { get; set; }

		[JsonProperty("privacyOnPreset")]
		public uint? PrivacyOnPreset { get; set; }

		[JsonProperty("privacyOffPreset")]
		public uint? PrivacyOffPreset { get; set; }

		[JsonProperty("pollTimeMs")]
		public long PollTimeMs { get; set; }

		[JsonProperty("warningTimeoutMs")]
		public long WarningTimeoutMs { get; set; }

		[JsonProperty("errorTimeoutMs")]
		public long ErrorTimeoutMs { get; set; }

        [JsonProperty("autoTracking")]
        public bool AutoTracking { get; set; }

        [JsonProperty("usePresetsForAutoTracking")]
        public bool UsePresetsForAutoTracking { get; set; }

		[JsonProperty("presets")]
		public List<ViscaCameraPresetConfig> Presets { get; set; }
	}

	public class ViscaCameraPresetConfig
	{
		[JsonProperty("index")]
		public uint Index { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

        [JsonProperty("viscaId")]
        public uint? ViscaId { get; set; }
	}
}