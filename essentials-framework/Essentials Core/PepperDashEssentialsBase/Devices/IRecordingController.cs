using System;
using System.Collections.Generic;
using PepperDash.Core;

namespace PepperDash.Essentials.Core.Recording
{
    public interface IRecordingController
    {
        StringFeedback StartRecordingStatus { get; set; }
        void StartRecording(string name, DateTime? endTime, Guid folderId);
        UserResults SearchUser(string searchText);
        KeyValuePair<string, Guid> GetUserFolder(Guid user);
        void CancelRecord();
        void ResetStartRecordingStatus();
    }

    public class UserResultsEntry
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
    }

    public class UserResults
    {
        public List<UserResultsEntry> Results { get; set; }

        public UserResults()
        {
            Results = new List<UserResultsEntry>();
        }
    }
}