using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.AudioDistribution;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace CrestronNaxAmp
{
    public class CrestronNaxAmp : CrestronGenericBridgeableBaseDevice
    {
        private readonly DmNaxAmpX300Base _amp;

        /// <summary>
        /// Mute feedback
        /// </summary>
        public BoolFeedback[] MuteFeedback { get; private set; }

        /// <summary>
        /// Volume feedback
        /// </summary>
        public IntFeedback[] VolumeFeedback { get; private set; }

        /// <summary>
        /// Zone name feedback
        /// </summary>
        public StringFeedback[] ZoneNameFeedback { get; private set; }

        private bool _ampFaultState;

        /// <summary>
        /// Amp fault feedback
        /// </summary>
        public BoolFeedback AmpFaultFeedback { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">String</param>
        /// <param name="name">String</param>
        /// <param name="ampBase"></param>
        public CrestronNaxAmp(string key, string name, DmNaxAmpX300Base ampBase)
            : base(key, name)
        {
            _amp = ampBase;

            //Link feedback
            _amp.OnlineStatusChange += IsOnlineFeedback_OutputChange;
            _amp.OnZoneChange += OnZoneChange;

            AmpFaultFeedback = new BoolFeedback(() => _ampFaultState);
            MuteFeedback = new BoolFeedback[_amp.Zones.Count];
            VolumeFeedback = new IntFeedback[_amp.Zones.Count];
            ZoneNameFeedback = new StringFeedback[_amp.Zones.Count];

            for (ushort i = 0; i < _amp.Zones.Count; i++)
            {
                ushort zone = i;
                MuteFeedback[zone] = new BoolFeedback(() => _amp.Zones[zone].MuteOnFeedback.BoolValue);
                VolumeFeedback[zone] = new IntFeedback(() => _amp.Zones[zone].VolumeFeedback.UShortValue);
                ZoneNameFeedback[zone] = new StringFeedback(() => _amp.Zones[zone].Name.StringValue);
            }
        }

        /// <summary>
        /// Link to API
        /// </summary>
        /// <param name="trilist">BasicTriList</param>
        /// <param name="joinStart">uint</param>
        /// <param name="joinMapKey">string</param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            CrestronNaxAmpDeviceJoinMap joinMap = new CrestronNaxAmpDeviceJoinMap(joinStart);

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            //From Plugin to Simpl
            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            AmpFaultFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AmpFault.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
            trilist.StringInput[joinMap.Presets.JoinNumber + 1].StringValue = "Default Volume";

            //From Simpl to Plugin
            trilist.SetSigTrueAction(joinMap.Presets.JoinNumber, () => SetDefaultVolume());

            //Link each zone to the bridge
            for (ushort i = 0; i < _amp.Zones.Count; i++)
            {
                ushort zone = i;

                MuteFeedback[i].LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + i]);
                VolumeFeedback[i].LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + i]);
                ZoneNameFeedback[i].LinkInputSig(trilist.StringInput[joinMap.ChannelName.JoinNumber + i]);

                trilist.UShortInput[joinMap.ChannelType.JoinNumber + i].UShortValue = 0;
                trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + i].BoolValue = true;

                trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + i, () => MuteToggle(zone));
                trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + i, () => MuteOn(zone));
                trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + i, () => MuteOff(zone));

                trilist.SetSigFalseAction(joinMap.EnableLevelSend.JoinNumber + i, () =>
                {
                    CrestronEnvironment.Sleep(100);
                    SetVolume(zone, trilist.UShortOutput[joinMap.ChannelVolume.JoinNumber + zone].UShortValue);
                });

                trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + i, u =>
                {
                    if (trilist.BooleanOutput[joinMap.EnableLevelSend.JoinNumber + zone].BoolValue)
                    {
                        SetVolume(zone, u);
                    }
                });
            }
        }

        private void UpdateAmpFaultStatus()
        {
            bool check = false;
            foreach (DmNaxXZone zone in _amp.Zones)
            {
                if (zone.DcOffsetFaultFeedback.BoolValue || zone.OverCurrentFaultFeedback.BoolValue ||
                    zone.OverTemperatureFaultFeedback.BoolValue || zone.OverOrUnderVoltageFaultFeedback.BoolValue)
                {
                    check = true;
                    Debug.ConsoleWithLog(0, this, "Amp Fault Detected");
                    break;
                }
            }

            _ampFaultState = check;
            AmpFaultFeedback.FireUpdate();
        }

        public void MuteOff(ushort zone)
        {
            if (zone < _amp.Zones.Count)
            {
                _amp.Zones[zone].MuteOff();
            }
        }

        public void MuteOn(ushort zone)
        {
            if (zone < _amp.Zones.Count)
            {
                _amp.Zones[zone].MuteOn();
            }
        }

        public void MuteToggle(ushort zone)
        {
            if (zone < _amp.Zones.Count)
            {
                if (_amp.Zones[zone].MuteOnFeedback.BoolValue)
                {
                    MuteOff(zone);
                }
                else
                {
                    MuteOn(zone);
                }
            }
        }

        public void SetVolume(ushort zone, ushort value)
        {
            if (zone < _amp.Zones.Count)
            {
                _amp.Zones[zone].Volume.UShortValue = value;
            }
        }

        public void SetDefaultVolume()
        {
            for (ushort i = 0; i < _amp.Zones.Count; i++)
            {
                _amp.Zones[i].Volume.UShortValue = _amp.Zones[i].StartupVolumeFeedback.UShortValue;
            }
        }

        private void IsOnlineFeedback_OutputChange(object dev, OnlineOfflineEventArgs args)
        {
            IsOnline.FireUpdate();
        }

        private void OnZoneChange(object dev, ZoneEventArgs args)
        {
            Debug.Console(2, this, "OnZoneChange Index:{0}, EventId:{1}", args.Index, args.EventId);
            if (args.Index >= _amp.Zones.Count)
            {
                return;
            }

            switch (args.EventId)
            {
                case ZoneEventIds.VolumeFeedbackEventId:
                    VolumeFeedback[args.Index].FireUpdate();
                    break;
                case ZoneEventIds.MuteOnFeedbackEventId:
                    MuteFeedback[args.Index].FireUpdate();
                    break;
                case ZoneEventIds.DcOffsetFaultEventId:
                case ZoneEventIds.OverCurrentFaultEventId:
                case ZoneEventIds.OverTemperatureFaultEventId:
                case ZoneEventIds.OverOrUnderVoltageFaultEventId:
                    UpdateAmpFaultStatus();
                    break;
            }
        }
    }

    #region Factory

    public class CrestronNaxAmpFactory : EssentialsDeviceFactory<CrestronNaxAmp>
    {
        public CrestronNaxAmpFactory()
        {
            TypeNames = new List<string>() { "x300residential", "x300commercial" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new NAX Amp Device");

            CrestronNaxAmpPropertiesConfig props =
                JsonConvert.DeserializeObject<CrestronNaxAmpPropertiesConfig>(dc.Properties.ToString());

            string type = dc.Type.ToLower();
            ControlPropertiesConfig control = props.Control;
            uint ipid = control.IpIdInt;

            switch (type)
            {
                case ("x300residential"):
                    return new CrestronNaxAmp(dc.Key, dc.Name, new DmNaxAmpX300Residential(ipid, Global.ControlSystem));
                case ("x300commercial"):
                    return new CrestronNaxAmp(dc.Key, dc.Name, new DmNaxAmpX300Commercial(ipid, Global.ControlSystem));
                default:
                    return null;
            }
        }
    }

    #endregion

    public class CrestronNaxAmpPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }

    /// <summary>
    /// Crestron Nax Amp Join Map
    /// </summary>
    public class CrestronNaxAmpDeviceJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Online Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("AmpFault")] public JoinDataComplete AmpFault =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Amp Fault Reported Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Name")] public JoinDataComplete Name =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("EnableLevelSend")] public JoinDataComplete EnableLevelSend =
            new JoinDataComplete(new JoinData { JoinNumber = 201, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Enable Level Sending from SIMPL",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVisible")] public JoinDataComplete ChannelVisible =
            new JoinDataComplete(new JoinData { JoinNumber = 201, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Visible Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteToggle")] public JoinDataComplete ChannelMuteToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 401, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute Toggle Set/Get",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteOn")] public JoinDataComplete ChannelMuteOn =
            new JoinDataComplete(new JoinData { JoinNumber = 601, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute On",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteOff")] public JoinDataComplete ChannelMuteOff =
            new JoinDataComplete(new JoinData { JoinNumber = 801, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute Off",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVolume")] public JoinDataComplete ChannelVolume =
            new JoinDataComplete(new JoinData { JoinNumber = 201, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Volume Set/Get",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.DigitalAnalog
                });

        [JoinName("ChannelType")] public JoinDataComplete ChannelType =
            new JoinDataComplete(new JoinData { JoinNumber = 401, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Type Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("ChannelName")] public JoinDataComplete ChannelName =
            new JoinDataComplete(new JoinData { JoinNumber = 201, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("Presets")] public JoinDataComplete Presets =
            new JoinDataComplete(new JoinData { JoinNumber = 101, JoinSpan = 100 },
                new JoinMetadata
                {
                    Description = "Preset Recall with Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.DigitalSerial
                });

        public CrestronNaxAmpDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(CrestronNaxAmpDeviceJoinMap))
        {
        }
    }
}