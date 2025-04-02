using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly CMutex endTimeMutex;
        private ushort _selectIndex;

        private DateTime? _currentMeetingEndTime;
        private DateTime? _nextRecordingStartTime;
        private string _currentUser;
        private Guid _currentUserGuid;
        private string _currentFolderName;
        private Guid _currentFolderGuid;
        private string _recordingName;
        private DateTime? _recordingEndTime;
        private readonly KeyValuePair<string, Guid>[] _usernames;
        private readonly List<DateTime> _endTimes = new List<DateTime>();
        private readonly int _userSearchSize;
        private readonly int _endTimeSize;
        private string _startRecorderStatus;
        private bool _subpageActive;
        private readonly CTimer _refreshEndTimesTimer;

        public StringFeedback CurrentUserFeedback { get; private set; }
        public StringFeedback CurrentFolderFeedback { get; private set; }
        public StringFeedback RecordingEndTime { get; private set; }
        public StringFeedback[] UserSearchFeedback { get; private set; }
        public StringFeedback[] EndTimesFeedback { get; private set; }

        public BoolFeedback[] EndTimeSelectedFeedback { get; private set; }
        public StringFeedback RecordingNameFeedback { get; private set; }

        private IRecordingController _recordingController;

        public StringFeedback StartRecordingStatusFeedback { get; private set; }
        public BoolFeedback StartRecordingFailedFeedback { get; private set; }

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
            endTimeMutex = new CMutex();
            _refreshEndTimesTimer = new CTimer(RefreshEndTimesCallback, Timeout.Infinite);

            _usernames = new KeyValuePair<string, Guid>[_userSearchSize];
            UserSearchFeedback = new StringFeedback[_userSearchSize];
            for (int i = 0; i < _userSearchSize; i++)
            {
                _usernames[i] = new KeyValuePair<string, Guid>("", Guid.Empty);
                int index = i;
                UserSearchFeedback[i] = new StringFeedback(() => _usernames[index].Key);
            }

            EndTimeSelectedFeedback = new BoolFeedback[_endTimeSize];
            EndTimesFeedback = new StringFeedback[_endTimeSize];
            for (int i = 0; i < _endTimeSize; i++)
            {
                int index = i;
                EndTimeSelectedFeedback[i] =
                    new BoolFeedback(() => index < _endTimes.Count && _endTimes[index] == _recordingEndTime);
                EndTimesFeedback[i] = new StringFeedback(() =>
                    index < _endTimes.Count &&
                    (_endTimes[index] < _nextRecordingStartTime || _nextRecordingStartTime == null)
                        ? _endTimes[index].ToString("t")
                        : "");
            }

            StartRecordingStatusFeedback =
                new StringFeedback(() => _startRecorderStatus ?? "");
            StartRecordingFailedFeedback = new BoolFeedback(() =>
                _startRecorderStatus != null && _startRecorderStatus.ToLower().Contains("failed"));
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

            UpdateEndTimesFeedback();
        }

        public void SetRecorderKey(string key)
        {
            if (_recordingController != null)
            {
                _recordingController.StartRecordingStatus.OutputChange -= UpdateRecordingStatusFeedback;
            }

            IKeyed device = DeviceManager.GetDeviceForKey(key);
            _recordingController = device as IRecordingController;
            if (_recordingController != null)
            {
                _recordingController.StartRecordingStatus.OutputChange += UpdateRecordingStatusFeedback;
            }

            Update();
        }

        private void UpdateRecordingStatusFeedback(object sender, FeedbackEventArgs args)
        {
            _startRecorderStatus = args.StringValue;
            StartRecordingStatusFeedback.FireUpdate();
            StartRecordingFailedFeedback.FireUpdate();
        }

        public void SearchUser(string name)
        {
            if (_recordingController == null)
            {
                return;
            }

            searchText = name;

            CrestronInvoke.BeginInvoke((o) =>
            {
                if (!searchLock)
                {
                    searchLock = true;
                }
                else
                {
                    return;
                }

                searchMutex.WaitForMutex();
                searchLock = false;
                name = searchText;
                try
                {
                    if (searchText.Length == 0)
                    {
                        ResetUser();
                    }

                    else
                    {
                        UserResults results = _recordingController.SearchUser(name);

                        if (results == null || results.Results.Count == 0)
                        {
                            _usernames[0] = new KeyValuePair<string, Guid>("No users found", Guid.Empty);

                            for (int i = 1; i < _userSearchSize; i++)
                            {
                                _usernames[i] = new KeyValuePair<string, Guid>(string.Empty, Guid.Empty);
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

                        CurrentUserFeedback.FireUpdate();
                        for (int i = 0; i < _userSearchSize; i++)
                        {
                            UserSearchFeedback[i].FireUpdate();
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

        public void SelectRecordingEndTime(ushort index)
        {
            _selectIndex = index;
            if (endTimeMutex.WaitForMutex(100))
            {
                try
                {
                    CrestronEnvironment.Sleep(250);
                    _recordingEndTime = _endTimes[_selectIndex];
                    RecordingEndTime.FireUpdate();
                    UpdateSelectedTimeFeedback();
                }
                finally
                {
                    endTimeMutex.ReleaseMutex();
                }
            }
        }

        public void SetRecordingName(string value)
        {
            _recordingName = value;
            RecordingNameFeedback.FireUpdate();
        }

        public void ClearAdhocData()
        {
            ResetUser();
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

        public void SetRecordingSubpageState(bool state)
        {
            _subpageActive = state;
            if (state == false)
            {
                ClearAdhocData();
            }
            else
            {
                _refreshEndTimesTimer.Reset(30000);
                GenerateNewEndTimes();
            }

            DefaultEndTime();
        }

        private void RefreshEndTimesCallback(object unused)
        {
            if (_subpageActive)
            {
                _refreshEndTimesTimer.Reset(30000);
                if (_endTimes[0] < DateTime.Now.AddMinutes(2))
                {
                    GenerateNewEndTimes();
                }
                else
                {
                    UpdateEndTimesFeedback();
                }
            }
        }

        private void GenerateNewEndTimes()
        {
            // Compute the next 5-minute rounded start time.
            DateTime start = GetFirstEndTime();

            endTimeMutex.WaitForMutex();
            try
            {
                _endTimes.Clear();
                // Generate 5-minute slots from the computed start until the end
                for (ushort i = 0; i < _endTimeSize; i++)
                {
                    _endTimes.Add(start);
                    start = start.AddMinutes(5);
                }

                UpdateEndTimesFeedback();
                UpdateSelectedTimeFeedback();
            }
            finally
            {
                endTimeMutex.ReleaseMutex();
            }
        }

        private DateTime GetFirstEndTime()
        {
            return RoundDownToPrevious5MinuteInterval(DateTime.Now.AddMinutes(2)).AddMinutes(5);
        }

        public void DefaultEndTime()
        {
            //Default recording to 1 hour from now
            DateTime endTime = RoundUpToNext5MinuteInterval(DateTime.Now).AddHours(1);

            // When meeting end time exists and is more than 5 minutes from now, use the meeting end time.
            if (_currentMeetingEndTime.HasValue && _currentMeetingEndTime.Value.AddMinutes(-5) > DateTime.Now)
            {
                endTime = _currentMeetingEndTime.Value;
            }

            // Now check the computed end time vs. the computed max end time
            if (_nextRecordingStartTime.HasValue && endTime > _nextRecordingStartTime.Value.AddMinutes(-5))
            {
                endTime = _nextRecordingStartTime.Value.AddMinutes(-5);
            }

            DateTime? validEndTime = GetNearestDateTime(endTime);

            _recordingEndTime = validEndTime;
            RecordingEndTime.FireUpdate();
            UpdateSelectedTimeFeedback();
        }

        public DateTime? GetNearestDateTime(DateTime target)
        {
            endTimeMutex.WaitForMutex();
            try
            {
                if (_endTimes == null || !_endTimes.Any())
                {
                    return null;
                }

                // Order the list by the absolute difference between each DateTime and the target,
                // and return the first (smallest difference)
                return _endTimes.OrderBy(dt => Math.Abs((dt - target).Ticks)).First();
            }
            finally
            {
                endTimeMutex.ReleaseMutex();
            }
        }

        private DateTime RoundDownToPrevious5MinuteInterval(DateTime dateTime)
        {
            // Calculate the minute part rounded down to the previous multiple of 5.
            int roundedMinutes = dateTime.Minute - (dateTime.Minute % 5);

            // Return a new DateTime with seconds and smaller units reset to 0.
            return new DateTime(
                dateTime.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                roundedMinutes,
                0,
                dateTime.Kind);
        }

        private DateTime RoundUpToNext5MinuteInterval(DateTime dateTime)
        {
            // Calculate the number of ticks in a 5-minute interval.
            long ticksPerFiveMinutes = TimeSpan.FromMinutes(5).Ticks;

            // Determine the remainder when dividing by the 5-minute interval.
            long remainderTicks = dateTime.Ticks % ticksPerFiveMinutes;

            // If the DateTime is already on a 5-minute boundary, return it as-is.
            if (remainderTicks == 0)
                return dateTime;

            // Calculate the number of ticks needed to reach the next interval.
            long ticksToAdd = ticksPerFiveMinutes - remainderTicks;

            // Return a new DateTime that is rounded up to the next 5-minute interval.
            return new DateTime(dateTime.Ticks + ticksToAdd, dateTime.Kind);
        }

        public void UpdateEndTimesFeedback()
        {
            endTimeMutex.WaitForMutex();
            try
            {
                for (int i = 0; i < _endTimeSize; i++)
                {
                    EndTimesFeedback[i].FireUpdate();
                }
            }
            finally
            {
                endTimeMutex.ReleaseMutex();
            }
        }

        private void UpdateSelectedTimeFeedback()
        {
            for (int i = 0; i < _endTimeSize; i++)
            {
                EndTimeSelectedFeedback[i].FireUpdate();
            }
        }

        public void SetCurrentMeetingEndTime(string time)
        {
            DateTime? temp;
            if (string.IsNullOrEmpty(time))
            {
                temp = null;
            }

            else
            {
                try
                {
                    temp = DateTime.Parse(time);
                }
                catch
                {
                    temp = null;
                }
            }

            _currentMeetingEndTime = temp;

            if (_subpageActive == false)
            {
                DefaultEndTime();
            }
        }

        public void SetNextRecordingStartTime(string time)
        {
            DateTime? temp;
            if (string.IsNullOrEmpty(time))
            {
                temp = null;
            }
            else
            {
                try
                {
                    temp = RoundDownToPrevious5MinuteInterval(DateTime.Parse(time));
                }
                catch
                {
                    temp = null;
                }
            }

            if (_subpageActive == false)
            {
                DefaultEndTime();
            }

            if (_nextRecordingStartTime != temp)
            {
                _nextRecordingStartTime = temp;
                UpdateEndTimesFeedback();
            }
        }

        public void CancelAdHoc()
        {
            if (_recordingController != null)
            {
                _recordingController.CancelRecord();
            }
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
            if (endTimeMutex != null) endTimeMutex.Dispose();
            if (_refreshEndTimesTimer != null) _refreshEndTimesTimer.Dispose();
        }
    }
}