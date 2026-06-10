using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.DSP.QscDsp
{
    /// <summary>
    /// QSC DSP Properties config class
    /// </summary>
    /// <remarks>
    /// These are key-value paris, string id, string type.
    /// Valid types are level and mute.
    /// Need to include the index values somehow.
    /// </remarks>
    /// <code>
    /// "key": "dsp-1",
    /// "name": "QSC Q-Sys DSP Plugin",
    /// "type": "qscdsp",
    /// "group": "plugin",
    /// "properties": {
    ///		"control": {
    ///			"method": "tcpIp",
    ///			"endOfLineString": "\n",
    ///			"deviceReadyResponse": "",
    ///			"tcpSshProperties": {
    ///				"address": "",
    ///				"port": 1702,
    ///				"username": "default",
    ///				"password": "",
    ///				"autoReconnect": true,
    ///				"autoReconnectIntervalMs": 5000
    ///			}
    ///		},
    ///		"prefix": "",
    ///		"levelControlBlocks": {},
    ///		"presets": {},
    ///		"dialerControlBlock": {},
    ///		"cameraControlBlocks": {}
    /// }
    /// </code>
    public class QscDspPropertiesConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }

        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("prefix")] public string Prefix { get; set; }

        [JsonProperty("levelControlBlocks")]
        public Dictionary<string, QscDspLevelControlBlockConfig> LevelControlBlocks { get; set; }

        [JsonProperty("presets")] public Dictionary<string, QscDspPresets> Presets { get; set; }
        
        [JsonProperty("monitoringPoints")]
        public List<QscDspMonitoringPointConfig> MonitoringPoints { get; set; }
    }

    /// <summary>
    /// QSC Presets Configurations
    /// This class is used for Level Control Blocks as well as Camera presets
    /// </summary>
    /// <remarks>
    /// LabelFeedback is not required in the JSON configuration.  It is used to return the defined label as a feedback on the bridge.
    /// </remarks>
    /// <code>
    /// "presets": {
    ///		"preset-key": {
    ///			"label": "Preset X",
    ///			"preset": "PRESET TAG"
    ///			"bank": "PRESET BANK",
    ///			"number": "PRESET NUMBER"
    ///		}
    /// }
    /// </code>
    public class QscDspPresets
    {
        // backer field
        private string _label;

        [JsonProperty("label")]
        public string Label
        {
            get { return _label; }
            set
            {
                _label = value;
                LabelFeedback.FireUpdate();
            }
        }

        [JsonProperty("preset")] public string Preset { get; set; }

        [JsonProperty("bank")] public string Bank { get; set; }

        [JsonProperty("number")] public int Number { get; set; }

        [JsonProperty("labelFeedback")] public StringFeedback LabelFeedback;

        /// <summary>
        /// Constructor
        /// </summary>
        public QscDspPresets()
        {
            LabelFeedback = new StringFeedback(() => Label);
        }
    }

    /// <summary>
    /// QSC Level Control Block Configuration 
    /// </summary>
    /// <code>
    /// "levelControlBlocks": {
    ///		"fader-key": {
    ///			"label": "Fader X",
    ///			"levelInstanceTag": "NAMED_CONTROL_VOL",
    ///			"muteInstanceTag": "NAMED_CONTROL_MUTE",
    ///			"disabled": false,
    ///			"hasLevel": true,
    ///			"hasMute": true,
    ///			"isMic": false,
    ///			"useAbsoluteValue": false,
    ///			"unmuteOnVolchange": true
    ///		}
    /// }
    /// </code>
    public class QscDspLevelControlBlockConfig
    {
        [JsonProperty("label")] public string Label { get; set; }

        [JsonProperty("levelInstanceTag")] public string LevelInstanceTag { get; set; }

        [JsonProperty("muteInstanceTag")] public string MuteInstanceTag { get; set; }

        [JsonProperty("disabled")] public bool Disabled { get; set; }

        [JsonProperty("isMic")] public bool IsMic { get; set; }

        [JsonProperty("permissions")] public int Permissions { get; set; }

        [JsonProperty("useAbsoluteValue")] public bool UseAbsoluteValue { get; set; }

        [JsonProperty("unmuteOnVolChange")] public bool UnmuteOnVolChange { get; set; }
    }
    
    public class QscDspMonitoringPointConfig
    {
        [JsonProperty("name")] public string Name { get; set; }
        
        [JsonProperty("instanceTag")] public string InstanceTag { get; set; }
    }
}