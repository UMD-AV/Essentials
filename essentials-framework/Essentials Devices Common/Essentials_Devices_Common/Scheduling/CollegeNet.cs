using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Net.Https;
using PepperDash.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Devices.Common.Scheduling
{
    public class CollegeNet : EssentialsDevice, IBridgeAdvanced, IDisposable
    {
        public event EventHandler MeetingsUpdated;
        public event EventHandler CurrentMeetingUpdated;
        public event EventHandler NextMeetingUpdated;
        public event EventHandler SpaceInfoUpdated;
        private List<Meeting> Meetings { get; set; }
        private string SpaceName { get; set; }
        private string Instructions { get; set; }
        private List<Feature> SpaceFeatures { get; set; }
        private CTimer _updateCurrentMeeting;
        private CTimer _scheduleUpdateTimer;
        private CTimer _scheduleTimeout;
        private uint _scheduleFailCount;
        private readonly Random _randomGenerator;
        private ushort _nextMeetingIndex;

        private readonly string _baseUrl = "https://webservices.collegenet.com/r25ws/wrd/umd/run/";

        private bool ScheduleOnline { get; set; }

        private CurrentMeeting _currentMeeting;

        public CurrentMeeting CurrentMeeting
        {
            get { return _currentMeeting; }
            private set
            {
                _currentMeeting = value;
                if (CurrentMeetingUpdated != null) CurrentMeetingUpdated(this, EventArgs.Empty);
            }
        }

        private Meeting _nextMeeting;

        public Meeting NextMeeting
        {
            get { return _nextMeeting; }
            private set
            {
                _nextMeeting = value;
                if (NextMeetingUpdated != null) NextMeetingUpdated(this, EventArgs.Empty);
            }
        }

        private readonly string _username;
        private readonly string _password;
        private int _spaceId;
        private string _roomName;
        private HttpsClient _secureClient;
        private readonly CMutex _meetingMutex;
        private readonly JsonSerializerSettings _jsonSettings;

        public CollegeNet(string key, string name, CollegeNetPropertiesConfig props) :
            base(key, name)
        {
            if (props.SpaceId > 0) _spaceId = props.SpaceId;

            _username = props.Username;
            _password = props.Password;
            if (!string.IsNullOrEmpty(props.Url)) _baseUrl = props.Url;

            _meetingMutex = new CMutex();
            _randomGenerator = new Random();
            _jsonSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironmentOnProgramStatusEventHandler;
        }

        public override bool CustomActivate()
        {
            BuildClient();
            ScheduleOnline = false;
            _scheduleTimeout = new CTimer(ScheduleTimeoutCallback, Timeout.Infinite);
            _scheduleUpdateTimer = new CTimer(ScheduleUpdateTimerCallback, 5000);
            _updateCurrentMeeting = new CTimer(UpdateCurrentMeetingCallback, Timeout.Infinite);
            ArmScheduleUpdateTimer();
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            CollegeNetJoinMap joinMap = new CollegeNetJoinMap(joinStart);

            if (bridge != null)
                bridge.AddJoinMap(Key, joinMap);
            else
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");

            //Events from SIMPL
            trilist.SetSigTrueAction(joinMap.RefreshReservations.JoinNumber, ManualGetTodaysReservations);
            trilist.SetSigTrueAction(joinMap.RefreshSpaceInfo.JoinNumber, GetSpaceInfo);
            trilist.SetStringSigAction(joinMap.SetRoomName.JoinNumber, SetRoomName);


            MeetingsUpdated += (o, a) =>
            {
                trilist.BooleanInput[joinMap.ScheduleOnline.JoinNumber].BoolValue = ScheduleOnline;
                uint count = 0;
                if (Meetings != null)
                    foreach (Meeting meeting in Meetings)
                    {
                        trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + count].BoolValue =
                            meeting.MeetingActive;
                        trilist.StringInput[joinMap.MeetingTitle.JoinNumber + count].StringValue = meeting.Title;
                        trilist.StringInput[joinMap.MeetingName.JoinNumber + count].StringValue = meeting.Name;
                        trilist.StringInput[joinMap.MeetingType.JoinNumber + count].StringValue = meeting.Type;
                        trilist.StringInput[joinMap.MeetingTime.JoinNumber + count].StringValue =
                            meeting.Start.ToString("h:mm tt") + " - " + meeting.End.ToString("h:mm tt");
                        count++;
                        if (count > 50)
                            break;
                    }

                for (uint i = count; i < 50; i++)
                {
                    trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + i].BoolValue = false;
                    trilist.StringInput[joinMap.MeetingTitle.JoinNumber + i].StringValue = "";
                    trilist.StringInput[joinMap.MeetingName.JoinNumber + i].StringValue = "";
                    trilist.StringInput[joinMap.MeetingType.JoinNumber + i].StringValue = "";
                    trilist.StringInput[joinMap.MeetingTime.JoinNumber + i].StringValue = "";
                }
            };

            CurrentMeetingUpdated += (o, a) =>
            {
                if (CurrentMeeting == null)
                {
                    trilist.StringInput[joinMap.CurrentMeetingTitle.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.CurrentMeetingName.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.CurrentMeetingOrganizerEmail.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.CurrentMeetingType.JoinNumber].StringValue = "";
                    trilist.BooleanInput[joinMap.CurrentMeetingActive.JoinNumber].BoolValue = false;
                    trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue = "";
                    trilist.UShortInput[joinMap.CurrentMeetingTimeRemaining.JoinNumber].UShortValue = 0;
                    trilist.StringInput[joinMap.CurrentMeetingTimeRemainingString.JoinNumber].StringValue = "";
                }
                else
                {
                    trilist.StringInput[joinMap.CurrentMeetingTitle.JoinNumber].StringValue = CurrentMeeting.Title;
                    trilist.StringInput[joinMap.CurrentMeetingName.JoinNumber].StringValue = CurrentMeeting.Name;
                    trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue =
                        CurrentMeeting.OrganizerName;
                    trilist.StringInput[joinMap.CurrentMeetingOrganizerEmail.JoinNumber].StringValue =
                        CurrentMeeting.OrganizerEmail;
                    trilist.StringInput[joinMap.CurrentMeetingType.JoinNumber].StringValue = CurrentMeeting.Type;
                    trilist.BooleanInput[joinMap.CurrentMeetingActive.JoinNumber].BoolValue =
                        CurrentMeeting.MeetingActive;
                    trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue =
                        CurrentMeeting.Start.ToString("h:mm tt");
                    trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue =
                        CurrentMeeting.End.ToString("h:mm tt");
                    trilist.UShortInput[joinMap.CurrentMeetingTimeRemaining.JoinNumber].UShortValue =
                        CurrentMeeting.TimeRemainingInMin;
                    trilist.StringInput[joinMap.CurrentMeetingTimeRemainingString.JoinNumber].StringValue =
                        CurrentMeeting.TimeRemainingString;
                }

                //Update active meeting feedback on list
                uint count = 0;
                if (Meetings != null)
                    foreach (Meeting meeting in Meetings)
                    {
                        trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + count].BoolValue =
                            meeting.MeetingActive;
                        count++;
                        if (count > 50)
                            break;
                    }

                for (uint i = count; i < 50; i++)
                    trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + i].BoolValue = false;
            };

            NextMeetingUpdated += (o, a) =>
            {
                if (NextMeeting == null)
                {
                    trilist.StringInput[joinMap.NextMeetingTitle.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.NextMeetingName.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.NextMeetingType.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue = "";
                    trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue = "";
                    trilist.UShortInput[joinMap.NextMeetingIndex.JoinNumber].UShortValue = 0;
                }
                else
                {
                    trilist.StringInput[joinMap.NextMeetingTitle.JoinNumber].StringValue = NextMeeting.Title;
                    trilist.StringInput[joinMap.NextMeetingName.JoinNumber].StringValue = NextMeeting.Name;
                    trilist.StringInput[joinMap.NextMeetingType.JoinNumber].StringValue = NextMeeting.Type;
                    trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue =
                        NextMeeting.Start.ToString("h:mm tt");
                    trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue =
                        NextMeeting.End.ToString("h:mm tt");
                    trilist.UShortInput[joinMap.NextMeetingIndex.JoinNumber].UShortValue =
                        (ushort)(_nextMeetingIndex + 1);
                }
            };

            SpaceInfoUpdated += (o, a) =>
            {
                uint count = 0;
                trilist.StringInput[joinMap.SpaceName.JoinNumber].StringValue = SpaceName;
                trilist.StringInput[joinMap.SpaceInstructions.JoinNumber].StringValue = Instructions;
                if (SpaceFeatures != null)
                    foreach (Feature feature in SpaceFeatures)
                    {
                        if (feature.Quantity > 1)
                            trilist.StringInput[joinMap.Features.JoinNumber + count].StringValue =
                                string.Format("{0} (x{1})", feature.Name, feature.Quantity);
                        else
                            trilist.StringInput[joinMap.Features.JoinNumber + count].StringValue = feature.Name;

                        count++;
                        if (count > 50)
                            break;
                    }

                trilist.UShortInput[joinMap.FeatureCount.JoinNumber].UShortValue = (ushort)count;
                for (uint i = count; i < 50; i++) trilist.StringInput[joinMap.Features.JoinNumber + i].StringValue = "";
            };
        }

        private void BuildClient()
        {
            try
            {
                _secureClient = new HttpsClient
                {
                    UserAgent = "crestron",
                    KeepAlive = false,
                    Accept = "application/json",
                    AllowAutoRedirect = true,
                    PeerVerification = false,
                    HostVerification = false
                };
            }
            catch
            {
                Debug.Console(0, this, "Error building http client");
            }
        }

        private void GetData(string data, string requestName)
        {
            try
            {
                Debug.Console(1, this, "Getting https: {0}", data);
                HttpsClientRequest req = new HttpsClientRequest();
                string auth = string.Format("Basic {0}",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes(_username + ":" + _password)));
                string url = string.Format("{0}{1}", _baseUrl, data);
                Debug.Console(1, this, "url: {0} auth: {1}", url, auth);
                req.Header.ContentType = "application/json";
                req.Header.SetHeaderValue("Authorization", auth);
                req.Encoding = Encoding.UTF8;
                req.RequestType = RequestType.Get;
                req.Url.Parse(url);
                _secureClient.DispatchAsyncEx(req, HttpsCallback, requestName);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception in getdata:{0}", ex);
            }
        }

        public void GetEvent(int id)
        {
            GetData(string.Format("event.json?event_id={0}", id), "Event");
        }

        private void GetTodaysReservations()
        {
            Debug.Console(1, this, "Getting reservations for spaceId {0}", _spaceId);
            if (_spaceId != 0)
            {
                _scheduleTimeout.Reset(20000);
                GetData(string.Format("reservations.json?space_id={0}", _spaceId), "Reservations");
            }
            else
            {
                GetSpaceId();
            }
        }

        public void ManualGetTodaysReservations()
        {
            Debug.Console(0, this, "Manually getting reservations for spaceId {0}", _spaceId);
            if (_spaceId != 0)
                GetData(string.Format("reservations.json?space_id={0}", _spaceId), "Reservations");
            else
                GetSpaceId();
        }

        public void GetSpaceInfo()
        {
            Debug.Console(1, this, "Getting space info for spaceId {0}", _spaceId);
            if (_spaceId != 0)
                GetData(string.Format("space.json?space_id={0}", _spaceId), "Space");
            else
                GetSpaceId();
        }

        public void SetRoomName(string roomName)
        {
            if (roomName != _roomName && roomName.Length > 3)
            {
                _roomName = roomName;
                GetSpaceId();
            }
        }

        private void GetSpaceId()
        {
            if (_roomName != null)
            {
                _scheduleTimeout.Reset(20000);
                Debug.Console(1, this, "Getting space id for room with name: {0}", _roomName);
                GetData(string.Format("spaces.json?name={0}", _roomName), "SpacesName");
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "Error getting space id - room name null");
            }
        }

        private void ScheduleTimeoutCallback(object o)
        {
            if (_scheduleFailCount < 5)
            {
                _scheduleFailCount++;
                Debug.ConsoleWithLog(0, this, "CollegeNet Schedule Timeout. Attempt {0}", _scheduleFailCount);
                CrestronEnvironment.Sleep(60000);
                GetTodaysReservations();
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "CollegeNet Schedule Timeout");
                ScheduleOnline = false;
                if (MeetingsUpdated != null) MeetingsUpdated(this, null);

                UpdateCurrentMeetingCallback(null);
            }
        }

        private void ArmScheduleUpdateTimer()
        {
            DateTime now = DateTime.Now;
            DateTime oneAm = DateTime.Today.AddHours(1);

            if (now >= oneAm) oneAm = oneAm.AddDays(1);

            int timeUntilOneAm = (int)(oneAm - now).TotalMilliseconds;
            int randomOffset = _randomGenerator.Next(0, 3600000); //Choose random offset within one hour
            _scheduleUpdateTimer.Reset(timeUntilOneAm + 60000 + randomOffset);
        }

        private void ScheduleUpdateTimerCallback(object o)
        {
            ArmScheduleUpdateTimer();
            _scheduleFailCount = 0;
            Meetings = new List<Meeting>();
            GetTodaysReservations();
        }

        private void UpdateCurrentMeetingCallback(object unused)
        {
            Meeting currentMeetingTemp = null;
            Meeting nextMeetingTemp = null;

            if (Meetings != null && Meetings.Count > 0)
            {
                //Recheck every minute for current meeting
                _updateCurrentMeeting.Reset(60000);

                ushort count = 0;
                foreach (Meeting m in Meetings)
                {
                    try
                    {
                        DateTime startMinus20;
                        //Get start time minus 20 minutes
                        if (m.Start < DateTime.MinValue + TimeSpan.FromMinutes(20))
                        {
                            Debug.ConsoleWithLog(0, this,
                                "Current meeting has min datetime start: {0} start {1} end {2}", m.Name, m.Start,
                                m.End);
                            startMinus20 = DateTime.MinValue;
                        }
                        else
                        {
                            startMinus20 = m.Start - TimeSpan.FromMinutes(20);
                        }

                        //Current meeting is valid if meeting starts in 20 minutes or is currently active
                        if (DateTime.Now >= startMinus20 && DateTime.Now <= m.End &&
                            (currentMeetingTemp == null || currentMeetingTemp.Start > m.Start))
                        {
                            currentMeetingTemp = m;
                        }
                        //If not the current meeting, make the next meeting if it occurs in the future and isn't later than the current "next meeting"
                        else if (DateTime.Now < m.Start && (nextMeetingTemp == null || nextMeetingTemp.Start > m.Start))
                        {
                            nextMeetingTemp = m;
                            _nextMeetingIndex = count;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.ConsoleWithLog(0, this, "Exception processing current meeting: {0}", e.Message);
                    }

                    count++;
                }
            }

            if (currentMeetingTemp == null)
            {
                if (CurrentMeeting != null)
                    CurrentMeeting = null;
            }
            else if (CurrentMeeting == null || currentMeetingTemp.Id != CurrentMeeting.Id)
            {
                CurrentMeeting = new CurrentMeeting(currentMeetingTemp);
                GetEvent(CurrentMeeting.Id);
            }
            else
            {
                if (CurrentMeetingUpdated != null) CurrentMeetingUpdated(this, EventArgs.Empty);
            }

            if (nextMeetingTemp == null)
            {
                if (NextMeeting != null)
                    NextMeeting = null;
            }
            else if (NextMeeting == null || nextMeetingTemp.Id != NextMeeting.Id)
            {
                NextMeeting = nextMeetingTemp;
            }
        }

        private void CrestronEnvironmentOnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

            Debug.Console(0, this, "Program stopping. Closing connection {0}", Key);
            Dispose();
            Debug.Console(0, this, "Closing connection {0} complete", Key);
        }

        private void HttpsCallback(HttpsClientResponse response, HTTPS_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                if (error != HTTPS_CALLBACK_ERROR.COMPLETED || response == null)
                {
                    Debug.ConsoleWithLog(0, this, "Https client callback error: {0}", error);
                    return;
                }

                Debug.Console(1, this, "Https client response code:{0}", response.Code.ToString());
                if (response.Code < 200 || response.Code >= 300)
                    Debug.ConsoleWithLog(0, this, "Https client callback code error: {0}", response.Code);
                else
                    ProcessFeedback((string)requestName, response.ContentString);
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, this, "Https client callback exception: {0}", ex.Message);
            }
        }

        private void ProcessFeedback(string requestName, string content)
        {
            Debug.Console(1, this, "Processing feedback:{0}", requestName);
            switch (requestName)
            {
                case "Reservations":
                {
                    try
                    {
                        _meetingMutex.WaitForMutex();
                        Meetings = new List<Meeting>();

                        ReservationsResponse response =
                            JsonConvert.DeserializeObject<ReservationsResponse>(content, _jsonSettings);
                        _scheduleTimeout.Stop();
                        ScheduleOnline = true;
                        _scheduleFailCount = 0;
                        if (response.Reservations != null && response.Reservations.Reservation != null)
                            foreach (Reservation reservation in response.Reservations.Reservation)
                                try
                                {
                                    bool matchExists = false;
                                    List<Meeting> matchesStart =
                                        Meetings.FindAll(m => m.Start == reservation.ReservationStartDt);
                                    if (matchesStart.Count > 0)
                                        matchExists = matchesStart.Exists(m => m.End == reservation.ReservationEndDt);

                                    if (!matchExists)
                                    {
                                        Meetings.Add(new Meeting
                                        {
                                            Id = reservation.EventId,
                                            Name = reservation.EventName != null
                                                ? SimplifyClassName(reservation.EventName)
                                                : "",
                                            Title = reservation.EventTitle ?? "",
                                            Start = reservation.ReservationStartDt,
                                            End = reservation.ReservationEndDt,
                                            Type = reservation.EventTypeName ?? ""
                                        });
                                    }
                                    else
                                    {
                                        string newName = SimplifyClassName(reservation.EventName);
                                        Meeting meeting =
                                            matchesStart.First(m => m.End == reservation.ReservationEndDt);
                                        Debug.Console(0, this,
                                            "New overlapping meeting: {0}, newName: {1}, length: {2}",
                                            meeting.Name, newName, meeting.Name.Length + newName.Length);
                                        if (meeting.Name.Length + newName.Length < 50 &&
                                            !meeting.Name.Contains(newName))
                                            meeting.Name = meeting.Name + "/" + newName;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.ConsoleWithLog(0, this, "Reservations processing exception: {0}", ex.Message);
                                }

                        if (SpaceFeatures == null || SpaceFeatures.Count == 0)
                            CrestronInvoke.BeginInvoke((o) =>
                            {
                                CrestronEnvironment.Sleep(10000);
                                GetSpaceInfo();
                            });
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "Reservations processing exception: {0}", ex.Message);
                    }
                    finally
                    {
                        _meetingMutex.ReleaseMutex();
                    }

                    if (MeetingsUpdated != null) MeetingsUpdated(this, null);

                    UpdateCurrentMeetingCallback(null);
                    break;
                }
                case "Event":
                    try
                    {
                        _meetingMutex.WaitForMutex();
                        EventsResponse response = JsonConvert.DeserializeObject<EventsResponse>(content);
                        if (CurrentMeeting.Id == response.Events.Event.EventId)
                        {
                            Contact c = null;
                            foreach (Role role in response.Events.Event.Role)
                                if (role.RoleName == "INSTRUCTOR")
                                {
                                    c = role.Contact;
                                    break;
                                }
                                else if (role.RoleName != "Scheduler")
                                {
                                    c = role.Contact;
                                }

                            if (c != null)
                            {
                                CurrentMeeting.OrganizerName = c.ContactFirstName + " " + c.ContactLastName;
                                CurrentMeeting.OrganizerEmail = c.Email.Replace("@g.umd.edu", "@umd.edu");
                                if (CurrentMeetingUpdated != null) CurrentMeetingUpdated(this, EventArgs.Empty);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "Reservations processing exception: {0}", ex.Message);
                    }
                    finally
                    {
                        _meetingMutex.ReleaseMutex();
                    }

                    break;
                case "Space":
                    try
                    {
                        SpaceResponse response = JsonConvert.DeserializeObject<SpaceResponse>(content);
                        SpaceFeatures = response.Spaces.Space[0].Features;
                        SpaceName = response.Spaces.Space[0].SpaceName;
                        Instructions = response.Spaces.Space[0].Instructions;
                        if (SpaceInfoUpdated != null) SpaceInfoUpdated(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "Spaces processing exception: {0}", ex.Message);
                    }

                    break;
                case "SpacesName":
                    try
                    {
                        SpaceResponse response = JsonConvert.DeserializeObject<SpaceResponse>(content);
                        if (response.Spaces.Space.Count == 1)
                        {
                            _spaceId = response.Spaces.Space[0].SpaceId;
                        }
                        else if (response.Spaces.Space.Count > 1)
                        {
                            if (response.Spaces.Space.Exists(s => s.SpaceName == _roomName))
                            {
                                Space resultSpace = response.Spaces.Space.Find(s => s.SpaceName == _roomName);
                                _spaceId = resultSpace.SpaceId;
                            }
                            else
                            {
                                _spaceId = response.Spaces.Space[0].SpaceId;
                                Debug.ConsoleWithLog(0, this, "SpacesName no exact match found for: {0}, using id {1}",
                                    _roomName, _spaceId);
                            }
                        }
                        else
                        {
                            Debug.ConsoleWithLog(0, this, "SpacesName no results found for: {0}", _roomName);
                        }

                        GetTodaysReservations();
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, this, "SpacesName processing exception for name: {0}", ex.Message);
                    }

                    break;
            }
        }

        private string SimplifyClassName(string name)
        {
            //Look for a string formatted like "ITAL 436 01011 XL 202408" and remove the last three groups
            string pattern = @" ([0-9|A-Z]{5}) ([A-Z]{2} )?([0-9]{6})";

            string result = Regex.Replace(name, pattern, "");
            return result.Trim();
        }

        public void Dispose()
        {
            if (_secureClient != null) _secureClient.Dispose();
            if (_updateCurrentMeeting != null) _updateCurrentMeeting.Dispose();
            if (_scheduleUpdateTimer != null) _scheduleUpdateTimer.Dispose();
            if (_scheduleTimeout != null) _scheduleTimeout.Dispose();
            if (_meetingMutex != null) _meetingMutex.Dispose();
        }
    }

    public class CollegeNetFactory : EssentialsDeviceFactory<CollegeNet>
    {
        public CollegeNetFactory()
        {
            TypeNames = new List<string> { "collegenet" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory attempting to create new CollegeNet Device");
            CollegeNetPropertiesConfig props =
                JsonConvert.DeserializeObject<CollegeNetPropertiesConfig>(dc.Properties.ToString());
            return new CollegeNet(dc.Key, dc.Name, props);
        }
    }

    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objecType)
        {
            return objecType == typeof(List<T>);
        }

        public override object ReadJson(JsonReader reader, Type objecType, object existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array) return token.ToObject<List<T>>();

            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class ReservationsResponse
    {
        [JsonProperty("reservations")] public Reservations Reservations { get; set; }
    }

    public class Reservations
    {
        [JsonProperty("reservation")]
        [JsonConverter(typeof(SingleOrArrayConverter<Reservation>))]
        public List<Reservation> Reservation { get; set; }
    }

    public class Reservation
    {
        [JsonProperty("event_id")] public int EventId { get; set; }

        [JsonProperty("event_title")] public string EventTitle { get; set; }

        [JsonProperty("event_name")] public string EventName { get; set; }

        [JsonProperty("event_type_name")] public string EventTypeName { get; set; }

        [JsonProperty("reservation_start_dt")] public DateTime ReservationStartDt { get; set; }

        [JsonProperty("reservation_end_dt")] public DateTime ReservationEndDt { get; set; }
    }

    public class EventsResponse
    {
        [JsonProperty("events")] public Events Events { get; set; }
    }

    public class Events
    {
        [JsonProperty("event")] public Event Event { get; set; }
    }

    public class Event
    {
        [JsonProperty("role")]
        [JsonConverter(typeof(SingleOrArrayConverter<Role>))]
        public List<Role> Role { get; set; }

        [JsonProperty("event_id")] public int EventId { get; set; }
    }

    public class Role
    {
        [JsonProperty("role_name")] public string RoleName { get; set; }

        [JsonProperty("contact")] public Contact Contact { get; set; }
    }

    public class Contact
    {
        [JsonProperty("contact_middle_name")] public string ContactMiddleName { get; set; }

        [JsonProperty("contact_name")] public string ContactName { get; set; }

        [JsonProperty("contact_last_name")] public string ContactLastName { get; set; }

        [JsonProperty("contact_first_name")] public string ContactFirstName { get; set; }

        [JsonProperty("email")] public string Email { get; set; }
    }

    public class CurrentMeeting : Meeting
    {
        public string OrganizerName { get; set; }
        public string OrganizerEmail { get; set; }

        public CurrentMeeting(Meeting meeting) : base(meeting)
        {
            OrganizerName = "";
            OrganizerEmail = "";
        }
    }

    public class Meeting
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        private string _type;

        public string Type
        {
            get { return _type; }
            set
            {
                if (value == "LEC")
                    _type = "Lecture";
                else if (value == "DIS")
                    _type = "Discussion";
                else
                    _type = value;
            }
        }

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public bool MeetingActive
        {
            get
            {
                bool testStart = Start <= DateTime.Now;
                bool testEnd = End >= DateTime.Now;
                return testStart && testEnd;
            }
        }

        public Meeting()
        {
            Id = 0;
            Title = "";
            Name = "";
            Type = "";
            Start = DateTime.MinValue;
            End = DateTime.MinValue;
        }

        public Meeting(Meeting m)
        {
            Id = m.Id;
            Title = m.Title;
            Name = m.Name;
            Type = m.Type;
            Start = m.Start;
            End = m.End;
        }

        public ushort TimeRemainingInMin
        {
            get
            {
                try
                {
                    double totalMinutes = Start <= DateTime.Now
                        ? End.Subtract(DateTime.Now).TotalMinutes
                        : End.Subtract(Start).TotalMinutes;

                    if (totalMinutes >= 0)
                        return (ushort)Math.Round(totalMinutes);
                    return 0;
                }
                catch (Exception e)
                {
                    Debug.ConsoleWithLog(0, "CollegeNet Meeting - error getting time remaining: {0}", e);
                    return 0;
                }
            }
        }

        public string TimeRemainingString
        {
            get
            {
                string hourTag = "";
                int hours = TimeRemainingInMin / 60;
                int minutes = TimeRemainingInMin % 60;
                if (hours > 1)
                    hourTag = "Hours";
                else if (hours == 1) hourTag = "Hour";

                string minTag = minutes == 1 ? "Minute" : "Minutes";

                return hourTag.Length == 0
                    ? string.Format("{0} {1}", minutes, minTag)
                    : string.Format("{0} {1} {2} {3}", hours, hourTag, minutes, minTag);
            }
        }
    }

    public class SpaceResponse
    {
        [JsonProperty("spaces")] public Spaces Spaces { get; set; }
    }

    public class Spaces
    {
        [JsonProperty("space")]
        [JsonConverter(typeof(SingleOrArrayConverter<Space>))]
        public List<Space> Space { get; set; }
    }

    public class Space
    {
        [JsonProperty("instructions")] public string Instructions { get; set; }
        [JsonProperty("space_name")] public string SpaceName { get; set; }
        [JsonProperty("space_id")] public int SpaceId { get; set; }

        [JsonProperty("feature")]
        [JsonConverter(typeof(SingleOrArrayConverter<Feature>))]
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("feature_name")] public string Name { get; set; }
        [JsonProperty("quantity")] public int Quantity { get; set; }
    }


    public class CollegeNetPropertiesConfig
    {
        [JsonProperty("username")] public string Username { get; set; }
        [JsonProperty("password")] public string Password { get; set; }
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("spaceId")] public int SpaceId { get; set; }
    }

    public class CollegeNetJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("Refresh Reservations")] public JoinDataComplete RefreshReservations = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh Reservations for Today",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Refresh Space Info")] public JoinDataComplete RefreshSpaceInfo = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh Space Information",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("CurrentMeetingActive")] public JoinDataComplete CurrentMeetingActive = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Active",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ScheduleOnline")] public JoinDataComplete ScheduleOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Schedule Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("MeetingActive")] public JoinDataComplete MeetingActive = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 51,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Meeting Active",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Analog

        [JoinName("CurrentMeetingTimeRemaining")]
        public JoinDataComplete CurrentMeetingTimeRemaining = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Time Remaining in Minutes",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("FeatureCount")] public JoinDataComplete FeatureCount = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Feature Count",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("NextMeetingIndex")] public JoinDataComplete NextMeetingIndex =
            new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "NextMeetingIndex", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Analog
                });

        #endregion


        #region Serial

        [JoinName("CurrentMeetingName")] public JoinDataComplete CurrentMeetingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SetRoomName")] public JoinDataComplete SetRoomName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Set room name for linking to CollegeNet ID",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingTitle")] public JoinDataComplete CurrentMeetingTitle = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingOrganizer")] public JoinDataComplete CurrentMeetingOrganizer = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Organizer",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingOrganizerEmail")]
        public JoinDataComplete CurrentMeetingOrganizerEmail = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Organizer Email",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingType")] public JoinDataComplete CurrentMeetingType = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingStartTime")] public JoinDataComplete CurrentMeetingStartTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Start Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingEndTime")] public JoinDataComplete CurrentMeetingEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting End Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingTimeRemainingString")]
        public JoinDataComplete CurrentMeetingTimeRemainingString = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Current Meeting Time Remaining String",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingName")] public JoinDataComplete NextMeetingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Next Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingTitle")] public JoinDataComplete NextMeetingTitle = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 12,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Next Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingType")] public JoinDataComplete NextMeetingType = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 13,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Next Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingStartTime")] public JoinDataComplete NextMeetingStartTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 14,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Next Meeting Start Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingEndTime")] public JoinDataComplete NextMeetingEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 15,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Next Meeting End Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SpaceName")] public JoinDataComplete SpaceName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Space Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SpaceInstructions")] public JoinDataComplete SpaceInstructions = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 22,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Space Instructions",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingName")] public JoinDataComplete MeetingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 51,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingTitle")] public JoinDataComplete MeetingTitle = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 101,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingType")] public JoinDataComplete MeetingType = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 151,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingTime")] public JoinDataComplete MeetingTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 201,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Meeting Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Features")] public JoinDataComplete Features = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 251,
                JoinSpan = 50
            },
            new JoinMetadata
            {
                Description = "Room Features",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        public CollegeNetJoinMap(uint joinStart)
            : base(joinStart, typeof(CollegeNetJoinMap))
        {
        }
    }
}