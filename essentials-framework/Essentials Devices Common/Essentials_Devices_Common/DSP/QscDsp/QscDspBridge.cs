using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;

namespace UmdEssentials.Devices.Common.DSP.QscDsp
{
    /// <summary>
    /// QSC DSP api extensions
    /// </summary>
    public static class QscDspDeviceApiExtensions
    {
        public static void LinkToApiExt(this QscDsp dspDevice, BasicTriList trilist, uint joinStart, string joinMapKey,
            EiscApiAdvanced bridge)
        {
            QscDspDeviceJoinMap joinMap = new QscDspDeviceJoinMap(joinStart);

            Debug.Console(1, dspDevice, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            ushort i = 1;

            dspDevice.IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = dspDevice.Name;

            foreach (KeyValuePair<string, QscDspLevelControl> channel in dspDevice.LevelControlPoints)
            {
                ushort x = i;
                Debug.Console(2, "QscChannel {0} connect", x);

                IBasicVolumeWithFeedback genericChannel = channel.Value;
                if (channel.Value.Enabled)
                {
                    trilist.UShortInput[joinMap.ChannelType.JoinNumber + x].UShortValue = (ushort)channel.Value.Type;
                    trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + x].BoolValue = true;
                    trilist.UShortInput[joinMap.ChannelPermissions.JoinNumber + x].UShortValue =
                        (ushort)channel.Value.Permissions;

                    genericChannel.MuteFeedback.LinkInputSig(
                        trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + x]);
                    genericChannel.VolumeLevelFeedback.LinkInputSig(
                        trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + x]);

                    trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + x,
                        genericChannel.MuteToggle);
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + x, genericChannel.MuteOn);
                    trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + x, genericChannel.MuteOff);
                    trilist.SetBoolSigAction(joinMap.ChannelVolumeUp.JoinNumber + x, genericChannel.VolumeUp);
                    trilist.SetBoolSigAction(joinMap.ChannelVolumeDown.JoinNumber + x,
                        genericChannel.VolumeDown);
                    trilist.SetSigFalseAction(joinMap.EnableLevelSend.JoinNumber + x, () =>
                    {
                        CrestronEnvironment.Sleep(500);
                        genericChannel.SetVolume(trilist.UShortOutput[joinMap.ChannelVolume.JoinNumber + x]
                            .UShortValue);
                    });

                    trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + x, u =>
                    {
                        if (trilist.BooleanOutput[joinMap.EnableLevelSend.JoinNumber + x].BoolValue)
                            genericChannel.SetVolume(u);
                    });
                }

                i++;
            }

            // Presets 
            i = 0;
            trilist.SetStringSigAction(joinMap.Presets.JoinNumber, dspDevice.RunPreset);
            foreach (QscDspPresets preset in dspDevice.PresetList)
            {
                ushort x = i;
                trilist.StringInput[joinMap.Presets.JoinNumber + x + 1].StringValue = preset.Label;
                trilist.SetSigTrueAction(joinMap.Presets.JoinNumber + x + 1, () => dspDevice.RunPresetNumber(x));
                i++;
            }

            i = 0;
            foreach (QscDspMonitoringPoint monitoringPoint in dspDevice.MonitoringControlPoints)
            {
                ushort x = i;
                trilist.StringInput[joinMap.MonitoringPointName.JoinNumber + x].StringValue = monitoringPoint.Name;
                monitoringPoint.IsOnline.LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.MonitoringPointOffline.JoinNumber + x]);
                i++;
            }

            while (i < joinMap.MonitoringPointOffline.JoinSpan)
            {
                trilist.StringInput[joinMap.MonitoringPointName.JoinNumber + i].StringValue = "";
                trilist.BooleanInput[joinMap.MonitoringPointOffline.JoinNumber + i].BoolValue = false;
                i++;
            }
        }
    }

    /// <summary>
    /// QSC DSP Join Map
    /// </summary>
    public class QscDspDeviceJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Online Feedback",
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

        [JoinName("MonitoringPointName")] public JoinDataComplete MonitoringPointName =
            new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 30 },
                new JoinMetadata
                {
                    Description = "Monitoring Point Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("MonitoringPointOffline")] public JoinDataComplete MonitoringPointOffline =
            new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 30 },
                new JoinMetadata
                {
                    Description = "Monitoring Point Offline Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("EnableLevelSend")] public JoinDataComplete EnableLevelSend =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Enable Level Sending from SIMPL",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVisible")] public JoinDataComplete ChannelVisible =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Visible Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteToggle")] public JoinDataComplete ChannelMuteToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute Toggle Set/Get",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteOn")] public JoinDataComplete ChannelMuteOn =
            new JoinDataComplete(new JoinData { JoinNumber = 600, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute On",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelMuteOff")] public JoinDataComplete ChannelMuteOff =
            new JoinDataComplete(new JoinData { JoinNumber = 800, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Mute Off",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVolumeUp")] public JoinDataComplete ChannelVolumeUp =
            new JoinDataComplete(new JoinData { JoinNumber = 1000, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Volume Up",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ChannelVolumeDown")] public JoinDataComplete ChannelVolumeDown =
            new JoinDataComplete(new JoinData { JoinNumber = 1200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Volume Down",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Presets")] public JoinDataComplete Presets =
            new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 100 },
                new JoinMetadata
                {
                    Description = "Preset Recall with Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.DigitalSerial
                });

        [JoinName("ChannelVolume")] public JoinDataComplete ChannelVolume =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Volume Set/Get",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.DigitalAnalog
                });

        [JoinName("ChannelType")] public JoinDataComplete ChannelType =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Type Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("ChannelPermissions")] public JoinDataComplete ChannelPermissions =
            new JoinDataComplete(new JoinData { JoinNumber = 800, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Permissions Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        [JoinName("ChannelName")] public JoinDataComplete ChannelName =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
                new JoinMetadata
                {
                    Description = "Channel Name Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        public QscDspDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(QscDspDeviceJoinMap))
        {
        }
    }
}