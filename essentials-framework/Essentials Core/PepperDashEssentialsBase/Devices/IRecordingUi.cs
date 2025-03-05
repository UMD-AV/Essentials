namespace PepperDash.Essentials.Core
{
    public interface IRecordingUi
    {
        BoolFeedback StartRecordingFailedFeedback { get; }
        BoolFeedback[] EndTimeSelectedFeedback { get; }
        StringFeedback CurrentUserFeedback { get; }
        StringFeedback CurrentFolderFeedback { get; }
        StringFeedback RecordingNameFeedback { get; }
        StringFeedback StartRecordingStatusFeedback { get; }
        StringFeedback[] UserSearchFeedback { get; }
        StringFeedback[] EndTimesFeedback { get; }
        void SetRecorderKey(string key);
        void SearchUser(string name);
        void SetRecordingName(string name);
        void SelectCurrentUser(ushort user);
        void SelectRecordingEndTime(ushort time);
        void ClearAdhocData();
        void StartRecording();
        void RefreshEndTimes();
        void SetCurrentMeetingEndTime(string time);
        void SetNextRecordingStartTime(string time);
        void CancelAdHoc();
    }
}