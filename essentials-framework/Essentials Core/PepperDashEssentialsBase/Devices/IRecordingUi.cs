namespace PepperDash.Essentials.Core
{
    public interface IRecordingUi
    {
        BoolFeedback[] EndTimeSelectedFeedback { get; }
        StringFeedback CurrentUserFeedback { get; }
        StringFeedback CurrentFolderFeedback { get; }
        StringFeedback RecordingNameFeedback { get; }
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
    }
}