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

		[JsonProperty("privacyOnPreset")]
		public uint PrivacyOnPreset { get; set; }

		[JsonProperty("privacyOffPreset")]
		public uint PrivacyOffPreset { get; set; }

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
	}
}