using System;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Routing;

namespace PepperDash.Essentials.Core
{
    public class Room : IKeyName, IBridgeAdvanced
    {
        public string Key { get; private set; }
        public string Name { get; private set; }
        private RoomConfig roomConfig;
        private RoomJoinMap joinMap;
        private BasicTriList roomTriList;

        public Room(RoomConfig config)
        {
            Key = config.Key;
            Name = config.RoomName;
            roomConfig = config;
            try
            {
                Router router = new Router(config);
                DeviceManager.AddDevice(router);
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, "Exception creating room router {0}: {1}", config.RoomName, e.Message);
            }
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            roomTriList = trilist;
            joinMap = new RoomJoinMap(joinStart);
            bridge.AddJoinMap(Key, joinMap);
            UpdateBridge();

            //Save from SIMPL
            trilist.SetStringSigAction(joinMap.RoomName.JoinNumber, SaveRoomName);
            trilist.SetUShortSigAction(joinMap.NumberOfMicBatteries.JoinNumber, SaveNumberOfMicBatteries);
            trilist.SetUShortSigAction(joinMap.OccShutdownTimeoutMinutes.JoinNumber, SaveOccShutdownTimeoutMinutes);
            trilist.SetUShortSigAction(joinMap.OccShutdownEnable.JoinNumber, SaveOccShutdownEnable);
        }

        private void SaveRoomName(string roomName)
        {
            if (roomConfig.RoomName != roomName)
            {
                roomConfig.RoomName = roomName;
                roomTriList.StringInput[joinMap.RoomName.JoinNumber].StringValue = roomName;
                ConfigWriter.UpdateRoomConfig(roomConfig);
            }
        }

        private void SaveNumberOfMicBatteries(ushort numberOfMicBatteries)
        {
            if (numberOfMicBatteries == 0) return;
            if (roomConfig.NumberOfMicBatteries != numberOfMicBatteries)
            {
                roomConfig.NumberOfMicBatteries = numberOfMicBatteries;
                roomTriList.UShortInput[joinMap.NumberOfMicBatteries.JoinNumber].UShortValue = numberOfMicBatteries;
                ConfigWriter.UpdateRoomConfig(roomConfig);
            }
        }

        private void SaveOccShutdownTimeoutMinutes(ushort occShutdownTimeoutMinutes)
        {
            if (occShutdownTimeoutMinutes == 0) return;
            if (roomConfig.OccShutdownMinutes != occShutdownTimeoutMinutes)
            {
                roomConfig.OccShutdownMinutes = occShutdownTimeoutMinutes;
                roomTriList.UShortInput[joinMap.OccShutdownTimeoutMinutes.JoinNumber].UShortValue =
                    occShutdownTimeoutMinutes;
                ConfigWriter.UpdateRoomConfig(roomConfig);
            }
        }

        private void SaveOccShutdownEnable(ushort occShutdownEnable)
        {
            if (occShutdownEnable == 0) return;
            bool occShutdownEnableBool = occShutdownEnable == 1;
            if (roomConfig.OccShutdownEnable != occShutdownEnableBool)
            {
                roomConfig.OccShutdownEnable = occShutdownEnableBool;
                roomTriList.UShortInput[joinMap.OccShutdownEnable.JoinNumber].UShortValue =
                    occShutdownEnableBool ? (ushort)1 : (ushort)2;
                ConfigWriter.UpdateRoomConfig(roomConfig);
            }
        }

        private void UpdateBridge()
        {
            if (roomTriList == null)
                return;

            //digital

            roomTriList.BooleanInput[joinMap.WallplateCapable.JoinNumber].BoolValue = roomConfig.WallplateCapable;
            roomTriList.BooleanInput[joinMap.ServiceNowEnable.JoinNumber].BoolValue = roomConfig.ServiceNowEnable;
            roomTriList.BooleanInput[joinMap.PhysicsDivisible.JoinNumber].BoolValue =
                roomConfig.PhysicsDivisible ?? false;
            roomTriList.BooleanInput[joinMap.AdvancedModeDefaultOn.JoinNumber].BoolValue =
                roomConfig.AdvancedModeDefaultOn;
            roomTriList.BooleanInput[joinMap.AdvancedModeToggleVisible.JoinNumber].BoolValue =
                roomConfig.AdvancedModeToggleVisible;

            //analog
            roomTriList.UShortInput[joinMap.OccShutdownTimeoutMinutes.JoinNumber].UShortValue =
                (ushort)roomConfig.OccShutdownMinutes;
            roomTriList.UShortInput[joinMap.DefaultSystemPreset.JoinNumber].UShortValue =
                (ushort)roomConfig.DefaultSystemPreset;
            roomTriList.UShortInput[joinMap.DefaultMicPreset.JoinNumber].UShortValue =
                (ushort)roomConfig.DefaultMicPreset;
            roomTriList.UShortInput[joinMap.NumberOfMicBatteries.JoinNumber].UShortValue =
                (ushort)roomConfig.NumberOfMicBatteries;
            roomTriList.UShortInput[joinMap.OccShutdownEnable.JoinNumber].UShortValue =
                roomConfig.OccShutdownEnable ? (ushort)1 : (ushort)2;

            //serial
            roomTriList.StringInput[joinMap.RoomName.JoinNumber].StringValue = roomConfig.RoomName;
            roomTriList.StringInput[joinMap.HelpText.JoinNumber].StringValue = roomConfig.HelpText;
            roomTriList.StringInput[joinMap.LightsKey.JoinNumber].StringValue = roomConfig.Lights01Key;
            roomTriList.StringInput[joinMap.LightsKey.JoinNumber + 1].StringValue = roomConfig.Lights02Key;
            roomTriList.StringInput[joinMap.FusionKey.JoinNumber].StringValue = roomConfig.FusionKey;
            roomTriList.StringInput[joinMap.OccSensorKey.JoinNumber].StringValue = roomConfig.OccSensor01Key;
            roomTriList.StringInput[joinMap.OccSensorKey.JoinNumber + 1].StringValue = roomConfig.OccSensor02Key;
            roomTriList.StringInput[joinMap.CodecKey.JoinNumber].StringValue = roomConfig.CodecKey;
            roomTriList.StringInput[joinMap.AvBridgeKey.JoinNumber].StringValue = roomConfig.AvBridgeKey;
            roomTriList.StringInput[joinMap.CameraControllerKey.JoinNumber].StringValue =
                roomConfig.CameraControllerKey;
            roomTriList.StringInput[joinMap.ShadesKey.JoinNumber].StringValue = roomConfig.Shades01Key;
            roomTriList.StringInput[joinMap.ShadesKey.JoinNumber + 1].StringValue = roomConfig.Shades02Key;
            roomTriList.StringInput[joinMap.MainFaderKey.JoinNumber].StringValue = roomConfig.MainFaderKey;
            roomTriList.StringInput[joinMap.PrivacyFaderKey.JoinNumber].StringValue = roomConfig.PrivacyFaderKey;
            roomTriList.StringInput[joinMap.OverflowInFaderKey.JoinNumber].StringValue = roomConfig.OverflowInFaderKey;
            roomTriList.StringInput[joinMap.OverflowOutFaderKey.JoinNumber].StringValue =
                roomConfig.OverflowOutFaderKey;
            roomTriList.StringInput[joinMap.OverflowKey.JoinNumber].StringValue = roomConfig.OverflowKey;
            roomTriList.StringInput[joinMap.ScheduleKey.JoinNumber].StringValue = roomConfig.ScheduleKey;
            roomTriList.StringInput[joinMap.MicDockKey.JoinNumber].StringValue = roomConfig.MicDock01Key;
            roomTriList.StringInput[joinMap.MicDockKey.JoinNumber + 1].StringValue = roomConfig.MicDock02Key;
            roomTriList.StringInput[joinMap.MicRxKey.JoinNumber].StringValue = roomConfig.MicRx01Key;
            roomTriList.StringInput[joinMap.MicRxKey.JoinNumber + 1].StringValue = roomConfig.MicRx02Key;
            roomTriList.StringInput[joinMap.MicRxKey.JoinNumber + 2].StringValue = roomConfig.MicRx03Key;
            roomTriList.StringInput[joinMap.MicRxKey.JoinNumber + 3].StringValue = roomConfig.MicRx04Key;
            roomTriList.StringInput[joinMap.RecorderKey.JoinNumber].StringValue = roomConfig.RecorderKey;

            //cameras
            if (roomConfig.Cameras != null)
            {
                int cameraCount = Math.Min(8, roomConfig.Cameras.Count);
                for (int i = 0; i < cameraCount; i++)
                {
                    if (roomConfig.Cameras != null && roomConfig.Cameras[i] != null)
                    {
                        roomTriList.StringInput[joinMap.CameraKey.JoinNumber + (uint)i].StringValue =
                            roomConfig.Cameras[i].Key;
                        int? camSource = roomConfig.Cameras[i].Source;
                        if (camSource != null)
                        {
                            roomTriList.UShortInput[joinMap.CameraSource.JoinNumber + (uint)i].UShortValue =
                                (ushort)camSource;
                        }

                        bool? camHide = roomConfig.Cameras[i].Hide;
                        if (camHide != null)
                        {
                            roomTriList.BooleanInput[joinMap.CameraHide.JoinNumber + (uint)i].BoolValue = (bool)camHide;
                        }
                    }
                }
            }

            //faders
            if (roomConfig.Faders != null)
            {
                int faderCount = Math.Min(8, roomConfig.Faders.Count);
                for (int i = 0; i < faderCount; i++)
                {
                    if (roomConfig.Faders != null && roomConfig.Faders[i] != null)
                    {
                        roomTriList.BooleanInput[joinMap.FaderVisibleUser.JoinNumber + (uint)i].BoolValue =
                            roomConfig.Faders[i].UserVisible;

                        roomTriList.BooleanInput[joinMap.FaderVisibleTech.JoinNumber + (uint)i].BoolValue =
                            roomConfig.Faders[i].TechVisible;

                        roomTriList.BooleanInput[joinMap.FaderMuteOnly.JoinNumber + (uint)i].BoolValue =
                            roomConfig.Faders[i].MuteOnly ?? false;

                        roomTriList.StringInput[joinMap.FaderName.JoinNumber + (uint)i].StringValue =
                            roomConfig.Faders[i].Name;

                        roomTriList.StringInput[joinMap.FaderKey.JoinNumber + (uint)i].StringValue =
                            roomConfig.Faders[i].Key;
                    }
                }
            }
        }

        public void RefreshConfig()
        {
            roomConfig = ConfigReader.ConfigObject.Rooms.First((config) => config.Key == Key);
            Name = roomConfig.RoomName;
            UpdateBridge();
        }
    }
}