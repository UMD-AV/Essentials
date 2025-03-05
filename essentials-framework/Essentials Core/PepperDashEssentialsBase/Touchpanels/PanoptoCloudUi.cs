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

        private string _currentUser;
        private Guid _currentUserGuid;
        private string _currentFolderName;
        private Guid _currentFolderGuid;
        private string _recordingName;
        private DateTime _recordingEndTime;
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

        public PanoptoCloudUi(int userSearchSize, int endTimeSize)
        {
            _userSearchSize = userSearchSize;
            _endTimeSize = endTimeSize;

            RecordingEndTime = new StringFeedback(() => _recordingEndTime.ToString("t"));
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

        //todo
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
            DeviceManager.GetDevices()
            IKeyed device = DeviceManager.GetDeviceForKey(key);
            _recordingController = device as IRecordingController;
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
            _recordingController.StartRecording(_recordingName, _recordingEndTime, _currentFolderGuid);
        }

        public void RefreshEndTimes()
        {
            throw new NotImplementedException();
        }

        public void SetCurrentMeetingEndTime(string time)
        {
            throw new NotImplementedException();
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