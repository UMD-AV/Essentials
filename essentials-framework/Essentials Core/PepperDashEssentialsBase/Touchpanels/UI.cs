using System;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Routing;

namespace PepperDash_Essentials_Core.Touchpanels
{
    public class UI : IKeyName, IBridgeAdvanced
    {
        public string Key { get; private set; }
        public string Name { get; private set; }
        private UiConfig uiConfig;
        private UiJoinMap joinMap;
        private BasicTriList uiTriList;
        private readonly IRecordingUi recordingUI;
        private const int _recordingUserSearchSize = 8;
        private const int _recordingEndTimeSize = 25;

        public UI(UiConfig config)
        {
            Key = config.Key;
            Name = config.Name;
            uiConfig = config;
            recordingUI = new PanoptoCloudUi(_recordingUserSearchSize, _recordingEndTimeSize);

            try
            {
                RoutingInterface routingInterface = new RoutingInterface(config);
                DeviceManager.AddDevice(routingInterface);
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, "Exception creating routing interface {0}: {1}", config.Key, e.Message);
            }
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            uiTriList = trilist;
            joinMap = new UiJoinMap(joinStart);
            bridge.AddJoinMap(Key, joinMap);

            trilist.SetSigTrueAction(joinMap.RecorderStartAdHoc.JoinNumber, recordingUI.StartRecording);
            trilist.SetSigTrueAction(joinMap.RecorderResetAdHoc.JoinNumber, recordingUI.ClearAdhocData);
            trilist.SetSigTrueAction(joinMap.RecorderRefreshEndTimes.JoinNumber, recordingUI.RefreshEndTimes);
            trilist.SetSigTrueAction(joinMap.RecorderCancelAdHoc.JoinNumber, recordingUI.CancelAdHoc);
            trilist.SetStringSigAction(joinMap.SetRecorderKey.JoinNumber, recordingUI.SetRecorderKey);
            trilist.SetStringSigAction(joinMap.RecorderCurrentUser.JoinNumber, recordingUI.SearchUser);
            trilist.SetStringSigAction(joinMap.SetRecordingName.JoinNumber, recordingUI.SetRecordingName);
            trilist.SetStringSigAction(joinMap.SetRecordingMeetingEndTime.JoinNumber,
                recordingUI.SetCurrentMeetingEndTime);
            trilist.SetStringSigAction(joinMap.SetNextRecordingStartTime.JoinNumber,
                recordingUI.SetNextRecordingStartTime);

            recordingUI.StartRecordingFailedFeedback.LinkInputSig(
                trilist.BooleanInput[joinMap.RecorderStartFailed.JoinNumber]);
            recordingUI.CurrentUserFeedback.LinkInputSig(trilist.StringInput[joinMap.RecorderCurrentUser.JoinNumber]);
            recordingUI.CurrentFolderFeedback.LinkInputSig(
                trilist.StringInput[joinMap.RecorderCurrentFolder.JoinNumber]);
            recordingUI.RecordingNameFeedback.LinkInputSig(trilist.StringInput[joinMap.SetRecordingName.JoinNumber]);
            recordingUI.StartRecordingStatusFeedback.LinkInputSig(
                trilist.StringInput[joinMap.StartRecordingStatus.JoinNumber]);

            for (ushort i = 0; i < _recordingUserSearchSize; i++)
            {
                ushort index = i;
                recordingUI.UserSearchFeedback[i]
                    .LinkInputSig(trilist.StringInput[joinMap.RecorderUserSearchResults.JoinNumber + i]);
                trilist.SetSigTrueAction(joinMap.RecorderSelectCurrentUser.JoinNumber + i,
                    () => recordingUI.SelectCurrentUser(index));
            }

            for (ushort i = 0; i < _recordingEndTimeSize; i++)
            {
                ushort index = i;
                recordingUI.EndTimesFeedback[i]
                    .LinkInputSig(trilist.StringInput[joinMap.RecorderEndTimeResults.JoinNumber + i]);
                recordingUI.EndTimeSelectedFeedback[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.RecorderSelectEndTime.JoinNumber + i]);
                trilist.SetSigTrueAction(joinMap.RecorderSelectEndTime.JoinNumber + i,
                    () => recordingUI.SelectRecordingEndTime(index));
            }

            UpdateBridge();
        }

        private void UpdateBridge()
        {
            //serial
            uiTriList.StringInput[joinMap.Name.JoinNumber].StringValue = uiConfig.Name;
            uiTriList.StringInput[joinMap.DefaultRoomKey.JoinNumber].StringValue = uiConfig.DefaultRoomKey;
            uiTriList.StringInput[joinMap.UserPassword.JoinNumber].StringValue = uiConfig.UserPassword;
            uiTriList.StringInput[joinMap.TechPassword.JoinNumber].StringValue = uiConfig.TechPassword;
            uiTriList.StringInput[joinMap.ScheduleKey.JoinNumber].StringValue = uiConfig.ScheduleKey;
        }

        public void RefreshConfig()
        {
            uiConfig = ConfigReader.ConfigObject.UIs.First(config => config.Key == Key);
            Name = uiConfig.Name;
            UpdateBridge();
        }
    }
}