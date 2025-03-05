using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Recording;

namespace PepperDash_Essentials_Core.Touchpanels
{
    public class PanoptoCloudUi : IRecordingUi, IDisposable
    {
        private bool searchLock;
        private string searchText;
        private readonly CMutex searchMutex;

        private DateTime? _currentMeetingEndTime;
        private DateTime? _nextRecordingStartTime;
        private string _currentUser;
        private Guid _currentUserGuid;
        private string _currentFolderName;
        private Guid _currentFolderGuid;
        private string _recordingName;
        private DateTime? _recordingEndTime;
        private readonly KeyValuePair<string, Guid>[] _usernames;
        private readonly DateTime?[] _endTimes;
        private readonly int _userSearchSize;
        private readonly int _endTimeSize;

        public StringFeedback CurrentUserFeedback { get; private set; }
        public StringFeedback CurrentFolderFeedback { get; private set; }
        public StringFeedback RecordingEndTime { get; private set; }
        public StringFeedback[] UserSearchFeedback { get; private set; }
        public StringFeedback[] EndTimesFeedback { get; private set; }

        public BoolFeedback[] EndTimeSelectedFeedback { get; private set; }
        public StringFeedback RecordingNameFeedback { get; private set; }

        private IRecordingController _recordingController;

        public StringFeedback StartRecordingStatusFeedback { get; private set; }

        public PanoptoCloudUi(int userSearchSize, int endTimeSize)
        {
            _userSearchSize = userSearchSize;
            _endTimeSize = endTimeSize;

            RecordingEndTime =
                new StringFeedback(() => _recordingEndTime != null ? ((DateTime)_recordingEndTime).ToString("t") : "");
            CurrentUserFeedback = new StringFeedback(() => _currentUser);
            CurrentFolderFeedback = new StringFeedback(() => _currentFolderName);
            RecordingNameFeedback = new StringFeedback(() => _recordingName);
            searchMutex = new CMutex();

            _usernames = new KeyValuePair<string, Guid>[_userSearchSize];
            UserSearchFeedback = new StringFeedback[_userSearchSize];
            for (int i = 0; i < _userSearchSize; i++)
            {
                _usernames[i] = new KeyValuePair<string, Guid>("", Guid.Empty);
                int index = i;
                UserSearchFeedback[i] = new StringFeedback(() => _usernames[index].Key);
            }

            _endTimes = new DateTime?[_endTimeSize];
            EndTimeSelectedFeedback = new BoolFeedback[_endTimeSize];
            EndTimesFeedback = new StringFeedback[_endTimeSize];
            for (int i = 0; i < _endTimeSize; i++)
            {
                int index = i;
                EndTimeSelectedFeedback[i] =
                    new BoolFeedback(() => _endTimes[index] != null && _endTimes[index] == _recordingEndTime);
                EndTimesFeedback[i] =
                    new StringFeedback(() =>
                        _endTimes[index] == null ? "" : ((DateTime)_endTimes[index]).ToString("t"));
            }
        }

        public void Update()
        {
            RecordingEndTime.FireUpdate();
            CurrentUserFeedback.FireUpdate();
            CurrentFolderFeedback.FireUpdate();
            RecordingNameFeedback.FireUpdate();
            for (int i = 0; i < _userSearchSize; i++)
            {
                UserSearchFeedback[i].FireUpdate();
            }

            for (int i = 0; i < _endTimeSize; i++)
            {
                UserSearchFeedback[i].FireUpdate();
                EndTimeSelectedFeedback[i].FireUpdate();
            }
        }

        public void SetRecorderKey(string key)
        {
            IKeyed device = DeviceManager.GetDeviceForKey(key);
            _recordingController = device as IRecordingController;
            if (_recordingController != null)
            {
                StartRecordingStatusFeedback = _recordingController.StartRecordingStatus;
            }

            Update();
        }

        public void SearchUser(string name)
        {
            if (_recordingController == null)
            {
                return;
            }

            CrestronInvoke.BeginInvoke((o) =>
            {
                if (!searchLock)
                {
                    searchText = name;
                    searchLock = true;
                }
                else
                {
                    searchText = name;
                    return;
                }

                searchMutex.WaitForMutex();
                searchLock = false;
                try
                {
                    if (searchText.Length == 0)
                    {
                        ResetUser();
                    }

                    else
                    {
                        UserResults results = _recordingController.SearchUser(name);

                        if (results == null)
                        {
                            _usernames[0] = new KeyValuePair<string, Guid>("No users found", Guid.Empty);
                            UserSearchFeedback[0].FireUpdate();
                            CurrentUserFeedback.FireUpdate();

                            for (int i = 1; i < _userSearchSize; i++)
                            {
                                _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                                UserSearchFeedback[i].FireUpdate();
                            }
                        }
                        else if (results.Results.Count > 0)
                        {
                            for (int i = 0; i < _userSearchSize; i++)
                            {
                                if (i < results.Results.Count)
                                {
                                    _usernames[i] =
                                        new KeyValuePair<string, Guid>(results.Results[i].Username,
                                            results.Results[i].Id);
                                }
                                else
                                {
                                    _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                                }
                            }

                            if (results.Results.Count == 1 &&
                                string.Equals(results.Results[0].Username, name,
                                    StringComparison.CurrentCultureIgnoreCase))
                            {
                                SelectCurrentUser(0);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.ConsoleWithLog(0, "Panopto UI error searching user {0}", ex.Message);
                }
                finally
                {
                    searchMutex.ReleaseMutex();
                }
            });
        }

        public void SelectCurrentUser(ushort user)
        {
            if (_recordingController == null)
            {
                return;
            }

            if (user < _usernames.Length && !_usernames[user].Value.Equals(Guid.Empty))
            {
                _currentUser = _usernames[user].Key;
                _currentUserGuid = _usernames[user].Value;
                KeyValuePair<string, Guid> result = _recordingController.GetUserFolder(_currentUserGuid);
                _currentFolderName = result.Key;
                _currentFolderGuid = result.Value;
                CurrentUserFeedback.FireUpdate();
                CurrentFolderFeedback.FireUpdate();
            }

            ResetUsernameSearchList();
        }

        public void SelectRecordingEndTime(ushort time)
        {
            _recordingEndTime = _endTimes[time];
            RecordingEndTime.FireUpdate();
        }

        public void SetRecordingName(string value)
        {
            _recordingName = value;
            RecordingNameFeedback.FireUpdate();
        }

        public void ClearAdhocData()
        {
            ResetUser();
            _recordingEndTime = DateTime.MinValue;
            RecordingEndTime.FireUpdate();
            _recordingName = "";
            RecordingNameFeedback.FireUpdate();
        }

        public void StartRecording()
        {
            if (_recordingController == null)
            {
                return;
            }

            _recordingController.StartRecording(_recordingName, _recordingEndTime, _currentFolderGuid);
        }

        public void DefaultEndTimes()
        {
            // Limit the computed time to no later than 4 hours from now.
            DateTime maxAllowedTime = DateTime.Now.AddHours(4);

            //Default recording to 1 hour from now
            DateTime endTime = DateTime.Now.AddHours(1);

            // When meeting end time exists, use the meeting end time.
            if (_currentMeetingEndTime.HasValue && _currentMeetingEndTime.Value > DateTime.Now)
            {
                endTime = _currentMeetingEndTime.Value;
            }

            // If the next recording exists, set the max time to that time
            if (_nextRecordingStartTime.HasValue)
            {
                DateTime nextRecordingMinus5 = _nextRecordingStartTime.Value.AddMinutes(-5);
                if (nextRecordingMinus5 < maxAllowedTime)
                {
                    maxAllowedTime = nextRecordingMinus5;
                }
            }

            // Now check the computed end time vs. the computed max end time
            if (endTime > maxAllowedTime)
            {
                endTime = maxAllowedTime;
            }

            _currentMeetingEndTime = endTime;
            List<DateTime> times = GenerateTimeSlots(maxAllowedTime, endTime);
            for (int i = 0; i < _endTimeSize; i++)
            {
                if (i < times.Count)
                {
                    _endTimes[i] = times[i];
                }
                else
                {
                    _endTimes[i] = null;
                }
            }

            RefreshEndTimes();
        }

        public List<DateTime> GenerateTimeSlots(DateTime maxTime, DateTime defaultTime)
        {
            // Compute the next 5-minute rounded start time.
            DateTime start = GetRoundedStartTime(DateTime.Now);

            List<DateTime> slots = new List<DateTime>();

            // Determine cutoff for 5-minute slots.
            // If the computed start is exactly on a quarter-hour (minutes % 15 == 0),
            // use the next quarter (i.e., add 15 minutes).
            // Otherwise, add enough minutes to
            // reach the next quarter, then add another full 15-minute interval.
            DateTime cutoff;
            if (start.Minute % 15 == 0)
            {
                cutoff = start.AddMinutes(15);
            }
            else
            {
                cutoff = start.AddMinutes((15 - (start.Minute % 15)) + 15);
            }

            // Generate 5-minute slots from the computed start until (but not including) the cutoff,
            while (start < cutoff && start <= maxTime)
            {
                slots.Add(start);
                start = start.AddMinutes(5);
            }

            // Generate 15-minute slots starting from cutoff.
            DateTime slot15 = cutoff;
            while (slot15 <= maxTime)
            {
                slots.Add(slot15);
                slot15 = slot15.AddMinutes(15);
            }

            // Ensure the default time is included.
            if (!slots.Contains(defaultTime))
            {
                slots.Add(defaultTime);
                slots.Sort();
            }

            return slots;
        }

        private DateTime GetRoundedStartTime(DateTime now)
        {
            // Truncate seconds and milliseconds.
            DateTime truncated = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            int remainder = now.Minute % 5;
            // Normally, add the minutes needed to get to the next 5-minute mark.
            int minutesToAdd = (remainder == 0) ? 5 : (5 - remainder);
            DateTime candidate = truncated.AddMinutes(minutesToAdd);

            // If the candidate is less than or equal to one minute away, skip to the following interval.
            if ((candidate - now) <= TimeSpan.FromMinutes(1))
            {
                candidate = candidate.AddMinutes(5);
            }

            return candidate;
        }

        public void RefreshEndTimes()
        {
            for (int i = 0; i < _endTimeSize; i++)
            {
                EndTimeSelectedFeedback[i].FireUpdate();
                EndTimesFeedback[i].FireUpdate();
            }
        }

        public void SetCurrentMeetingEndTime(string time)
        {
            if (string.IsNullOrEmpty(time))
            {
                _currentMeetingEndTime = null;
            }

            try
            {
                _currentMeetingEndTime = DateTime.Parse(time);
            }
            catch
            {
                _currentMeetingEndTime = null;
            }

            DefaultEndTimes();
        }

        public void SetNextRecordingStartTime(string time)
        {
            if (string.IsNullOrEmpty(time))
            {
                _nextRecordingStartTime = null;
            }

            try
            {
                _nextRecordingStartTime = DateTime.Parse(time);
            }
            catch
            {
                _nextRecordingStartTime = null;
            }

            DefaultEndTimes();
        }

        private void ResetUsernameSearchList()
        {
            for (int i = 0; i < _userSearchSize; i++)
            {
                _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
                UserSearchFeedback[i].FireUpdate();
            }
        }

        private void ResetUser()
        {
            _currentUser = string.Empty;
            _currentUserGuid = Guid.Empty;
            CurrentUserFeedback.FireUpdate();
            ResetCurrentFolder();
            ResetUsernameSearchList();
        }

        private void ResetCurrentFolder()
        {
            _currentFolderGuid = Guid.Empty;
            _currentFolderName = string.Empty;
            CurrentFolderFeedback.FireUpdate();
        }

        public void Dispose()
        {
            if (searchMutex != null) searchMutex.Dispose();
        }
    }
}