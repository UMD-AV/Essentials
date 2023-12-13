using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Devices.Common.Scheduling
{
    public class CollegeNet : EssentialsDevice, IBridgeAdvanced
    {
        public event EventHandler MeetingsUpdated;
        public event EventHandler CurrentMeetingUpdated;
        public event EventHandler NextMeetingUpdated;
        public event EventHandler SpaceInfoUpdated;
        public List<Meeting> Meetings { get; private set; }
        public string SpaceName { get; private set; }
        public string Instructions { get; private set; }
        public List<Feature> SpaceFeatures { get; private set; }
        private CTimer updateCurrentMeeting;
        private CTimer scheduleUpdateTimer;
        private CTimer scheduleTimeout;
        private uint scheduleFailCount;

        public bool ScheduleOnline { get; private set; }

        private CurrentMeeting _currentMeeting;
        public CurrentMeeting CurrentMeeting
        {
            get { return _currentMeeting; }
            private set
            {
                _currentMeeting = value;
                if (CurrentMeetingUpdated != null)
                {
                    CurrentMeetingUpdated(this, new EventArgs());
                }
            }
        }

        private Meeting _nextMeeting;
        public Meeting NextMeeting
        {
            get { return _nextMeeting; }
            private set
            {
                _nextMeeting = value;
                if (NextMeetingUpdated != null)
                {
                    NextMeetingUpdated(this, new EventArgs());
                }
            }
        }

        private string username;
        private string password;
        private string spaceId;
        private HttpsClient secureClient;
        private CMutex meetingMutex;
        
		public CollegeNet(string key, string name, CollegeNetPropertiesConfig props) :
            base(key, name)
        {
            this.spaceId = props.spaceId;
			this.username = props.username;
			this.password = props.password;
            meetingMutex = new CMutex();
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironmentOnProgramStatusEventHandler;
        }

        public override bool CustomActivate()
        {
            BuildClient();
            ScheduleOnline = false;
            scheduleTimeout = new CTimer(scheduleTimeoutCallback, Crestron.SimplSharp.Timeout.Infinite);
            scheduleUpdateTimer = new CTimer(scheduleUpdateTimerCallback, 5000);
            updateCurrentMeeting = new CTimer(UpdateCurrentMeetingCallback, Crestron.SimplSharp.Timeout.Infinite);
            armScheduleUpdateTimer();
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            CollegeNetJoinMap joinMap = new CollegeNetJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this, "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }
            
            //Events from SIMPL
            trilist.SetSigTrueAction(joinMap.RefreshReservations.JoinNumber, ManualGetTodaysReservations);
            trilist.SetSigTrueAction(joinMap.RefreshSpaceInfo.JoinNumber, GetSpaceInfo);


            MeetingsUpdated += (o, a) =>
            {
                trilist.BooleanInput[joinMap.ScheduleOnline.JoinNumber].BoolValue = ScheduleOnline;
                uint count = 0;
                if (Meetings != null)
                {
                    foreach (var meeting in Meetings)
                    {
                        trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + count].BoolValue = meeting.MeetingActive;
                        trilist.StringInput[joinMap.MeetingTitle.JoinNumber + count].StringValue = meeting.Title;
                        trilist.StringInput[joinMap.MeetingName.JoinNumber + count].StringValue = meeting.Name;
                        trilist.StringInput[joinMap.MeetingType.JoinNumber + count].StringValue = meeting.Type;
                        trilist.StringInput[joinMap.MeetingTime.JoinNumber + count].StringValue = meeting.Start.ToShortTimeString() + " - " + meeting.End.ToShortTimeString();
                        count++;
                        if (count > 50)
                            break;
                    }
                }
                for(uint i = count; i < 50; i++)
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
                    trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue = CurrentMeeting.OrganizerName;
                    trilist.StringInput[joinMap.CurrentMeetingOrganizerEmail.JoinNumber].StringValue = CurrentMeeting.OrganizerEmail;
                    trilist.StringInput[joinMap.CurrentMeetingType.JoinNumber].StringValue = CurrentMeeting.Type;
                    trilist.BooleanInput[joinMap.CurrentMeetingActive.JoinNumber].BoolValue = CurrentMeeting.MeetingActive;
                    trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue = CurrentMeeting.Start.ToShortTimeString();
                    trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue = CurrentMeeting.End.ToShortTimeString();
                    trilist.UShortInput[joinMap.CurrentMeetingTimeRemaining.JoinNumber].UShortValue = CurrentMeeting.TimeRemainingInMin;
                    trilist.StringInput[joinMap.CurrentMeetingTimeRemainingString.JoinNumber].StringValue = CurrentMeeting.TimeRemainingString;
                }

                //Update active meeting feedback on list
                uint count = 0;
                if (Meetings != null)
                {
                    foreach (var meeting in Meetings)
                    {
                        trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + count].BoolValue = meeting.MeetingActive;
                        count++;
                        if (count > 50)
                            break;
                    }
                }
                for (uint i = count; i < 50; i++)
                {
                    trilist.BooleanInput[joinMap.MeetingActive.JoinNumber + i].BoolValue = false;
                }
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
                }
                else
                {
                    trilist.StringInput[joinMap.NextMeetingTitle.JoinNumber].StringValue = NextMeeting.Title;
                    trilist.StringInput[joinMap.NextMeetingName.JoinNumber].StringValue = NextMeeting.Name;
                    trilist.StringInput[joinMap.NextMeetingType.JoinNumber].StringValue = NextMeeting.Type;
                    trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue = NextMeeting.Start.ToShortTimeString();
                    trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue = NextMeeting.End.ToShortTimeString();
                }
            };

            SpaceInfoUpdated += (o, a) =>
            {
                uint count = 0;
                trilist.StringInput[joinMap.SpaceName.JoinNumber + count].StringValue = SpaceName;
                trilist.StringInput[joinMap.SpaceInstructions.JoinNumber + count].StringValue = Instructions;
                if (SpaceFeatures != null)
                {
                    foreach (var feature in SpaceFeatures)
                    {
                        if (feature.Quantity > 1)
                        {
                            trilist.StringInput[joinMap.Features.JoinNumber + count].StringValue = string.Format("{0} (x{1})", feature.Name, feature.Quantity); 
                        }
                        else
                        {
                            trilist.StringInput[joinMap.Features.JoinNumber + count].StringValue = feature.Name;
                        }
                        count++;
                        if (count > 50)
                            break;
                    }
                }
                trilist.UShortInput[joinMap.FeatureCount.JoinNumber].UShortValue = (ushort)count;
                for (uint i = count; i < 50; i++)
                {
                    trilist.StringInput[joinMap.Features.JoinNumber + i].StringValue = "";
                }
            };
        }

        private void BuildClient()
        {
            try
            {
                secureClient = new HttpsClient()
                {
                    UserAgent = "crestron",
                    KeepAlive = false,
                    Accept = "application/json",
                    AllowAutoRedirect = false
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
                Debug.Console(0, this, "Getting https: {0}", data);
                var req = new HttpsClientRequest();
                string auth = string.Format("Basic {0}", Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(username + ":" + password)));
                string url = string.Format("https://webservices.collegenet.com/r25ws/wrd/umd/run/{0}", data);
                Debug.Console(0, this, "url: {0} auth: {1}", url, auth);
                req.Header.ContentType = "application/json";
                req.Header.SetHeaderValue("Authorization", auth);
                req.Encoding = Encoding.UTF8;
                req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                req.Url.Parse(url);
                secureClient.DispatchAsyncEx(req, HttpsCallback, requestName);
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
            Debug.Console(0, this, "Getting reservations for spaceId {0}", spaceId != null ? spaceId : "null");
            if (spaceId != null)
            {
                scheduleTimeout.Reset(10000);
                GetData(string.Format("reservations.json?space_id={0}", spaceId), "Reservations");
            }
        }

        public void ManualGetTodaysReservations()
        {
            Debug.Console(0, this, "Manually getting reservations for spaceId {0}", spaceId != null ? spaceId : "null");
            if (spaceId != null)
            {
                GetData(string.Format("reservations.json?space_id={0}", spaceId), "Reservations");
            }
        }

        public void GetSpaceInfo()
        {
            Debug.Console(0, this, "Getting space info for spaceId {0}", spaceId != null ? spaceId : "null");
            if (spaceId != null)
            {
                GetData(string.Format("space.json?space_id={0}", spaceId), "Space");
            }
        }

        private void scheduleTimeoutCallback(object o)
        {
            if (scheduleFailCount < 5)
            {
                scheduleFailCount++;
                Debug.Console(0, this, "CollegeNet Schedule Timeout. Attempt {0}", scheduleFailCount);
                GetTodaysReservations();
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "CollegeNet Schedule Timeout");
                ScheduleOnline = false;
                if (MeetingsUpdated != null)
                {
                    MeetingsUpdated(this, null);
                }
                UpdateCurrentMeetingCallback(null);
            }
        }

        private void armScheduleUpdateTimer()
        {
            DateTime now = DateTime.Now;
            DateTime oneAM = DateTime.Today.AddHours(1);

            if (now >= oneAM)
            {
                oneAM = oneAM.AddDays(1);
            }

            int timeUntilOneAM = (int)(oneAM - now).TotalMilliseconds;
            scheduleUpdateTimer.Reset(timeUntilOneAM + 60000);
        }

        private void scheduleUpdateTimerCallback(object o)
        {
            armScheduleUpdateTimer();
            scheduleFailCount = 0;
            Meetings = new List<Meeting>();
            GetTodaysReservations();
            GetSpaceInfo();
        }

        private void UpdateCurrentMeetingCallback(object unused)
        {
            Meeting _currentMeetingTemp = null;
            Meeting _nextMeetingTemp = null;

            if (Meetings != null && Meetings.Count > 0)
            {
                //Recheck every minute for current meeting
                updateCurrentMeeting.Reset(60000);

                foreach (Meeting m in Meetings)
                {
                    //Check for current meeting
                    //Current meeting is valid if meeting starts in 20 minutes or is currently active
                    if (m.Type != "Maintenance" && DateTime.Now >= (m.Start - TimeSpan.FromMinutes(20)) && DateTime.Now <= m.End && (_currentMeetingTemp == null || _currentMeetingTemp.Start > m.Start))
                    {
                        _currentMeetingTemp = m;
                    }
                    //If not the current meeting, make the next meeting if it occurs in the future and isn't later than the current "next meeting"
                    else if (m.Type != "Maintenance" && DateTime.Now < m.Start && (_nextMeetingTemp == null || _nextMeetingTemp.Start > m.Start))
                    {
                        _nextMeetingTemp = m;
                    }
                }
            }

            if (_currentMeetingTemp == null)
            {
                if (CurrentMeeting != null)
                    CurrentMeeting = null;
            }
            else if (CurrentMeeting == null || (_currentMeetingTemp.Id != CurrentMeeting.Id))
            {
                CurrentMeeting = new CurrentMeeting(_currentMeetingTemp);
                GetEvent(CurrentMeeting.Id);
            }
            else
            {
                if (CurrentMeetingUpdated != null)
                {
                    CurrentMeetingUpdated(this, new EventArgs());
                }
            }

            if (_nextMeetingTemp == null)
            {
                if (NextMeeting != null)
                    NextMeeting = null;
            }
            else if (NextMeeting == null || (_nextMeetingTemp.Id != NextMeeting.Id))
            {
                NextMeeting = _nextMeetingTemp;
            }
        }

        private void CrestronEnvironmentOnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;

            secureClient.Dispose();
        }

        private void HttpsCallback(HttpsClientResponse response, HTTPS_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                if (error != HTTPS_CALLBACK_ERROR.COMPLETED || response == null)
                {
                    Debug.Console(0, this, "Https client callback error: {0}", error);
                    return;
                }
                
                Debug.Console(1, this, "Https client response code:{0}", response.Code.ToString());
                if (response.Code < 200 || response.Code >= 300)
                {
                    Debug.Console(0, this, "Https client callback code error: {0}", response.Code);
                    return;
                }
                else
                {
                    ProcessFeedback((string)requestName, response.ContentString);                        
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Https client callback exception: {0}", ex.Message);
            }
        }

        private void ProcessFeedback(string requestName, string content)
        {
            Debug.Console(0, this, "Processing feedback:{0}", requestName);
            if (requestName == "Reservations")
            {
                try
                {
                    meetingMutex.WaitForMutex();
                    Meetings = new List<Meeting>();
                    ReservationsResponse response = JsonConvert.DeserializeObject<ReservationsResponse>(content);
                    scheduleTimeout.Stop();
                    ScheduleOnline = true;
                    scheduleFailCount = 0;
                    foreach (Reservation reservation in response.reservations.reservation)
                    {
                        try
                        {
                            bool matchExists = false;
                            var matchesStart = Meetings.FindAll(m => m.Start == reservation.reservation_start_dt);
                            if (matchesStart.Count > 0)
                            {
                                matchExists = matchesStart.Exists(m => m.End == reservation.reservation_end_dt);
                            }
                            if (!matchExists)
                            {
                                Meetings.Add(new Meeting()
                                {
                                    Id = reservation.event_id,
                                    Name = reservation.event_name,
                                    Title = reservation.event_title,
                                    Start = reservation.reservation_start_dt,
                                    End = reservation.reservation_end_dt,
                                    Type = reservation.event_type_name
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "Reservations processing exception: {0}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Reservations processing exception: {0}", ex.Message);
                }
                finally
                {
                    meetingMutex.ReleaseMutex();
                }

                if (MeetingsUpdated != null)
                {
                    MeetingsUpdated(this, null);
                }
                UpdateCurrentMeetingCallback(null);
            }
            else if (requestName == "Event")
            {
                try
                {
                    meetingMutex.WaitForMutex();
                    EventsResponse response = JsonConvert.DeserializeObject<EventsResponse>(content);
                    if(CurrentMeeting.Id == response.events._event.event_id)
                    {
                        Contact c = null;
                        foreach (Role role in response.events._event.role)
                        {                           
                            if (role.role_name == "INSTRUCTOR")
                            {
                                c = role.contact;
                                break;
                            }
                            else if (role.role_name != "Scheduler")
                            {
                                c = role.contact;
                            }
                        }
                        if (c != null)
                        {
                            CurrentMeeting.OrganizerName = c.contact_first_name + " " + c.contact_last_name;
                            CurrentMeeting.OrganizerEmail = c.email;
                            if (CurrentMeetingUpdated != null)
                            {
                                CurrentMeetingUpdated(this, new EventArgs());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Reservations processing exception: {0}", ex.Message);
                }
                finally
                {
                    meetingMutex.ReleaseMutex();
                }
            }
            else if (requestName == "Space")
            {
                try
                {
                    SpaceResponse response = JsonConvert.DeserializeObject<SpaceResponse>(content);
                    SpaceFeatures = response.Spaces.Space.Features;
                    SpaceName = response.Spaces.Space.SpaceName;
                    Instructions = response.Spaces.Space.Instructions;
                    if (SpaceInfoUpdated != null)
                    {
                        SpaceInfoUpdated(this, new EventArgs());
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Spaces processing exception: {0}", ex.Message);
                }
            }
        }
    }

    public class CollegeNetFactory : EssentialsDeviceFactory<CollegeNet>
    {
        public CollegeNetFactory()
        {
            TypeNames = new List<string>() { "collegenet" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory attempting to create new CollegeNet Device");
            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<CollegeNetPropertiesConfig>(
                dc.Properties.ToString());
            return new CollegeNet(dc.Key, dc.Name, props);
        }
    }

    public class ReservationsResponse
    {
        public Reservations reservations { get; set; }
    }

    public class Reservations
    {
        public List<Reservation> reservation { get; set; }
    }

    public class Reservation
    {
        public int event_id { get; set; }
        public string event_title { get; set; }
        public string event_name { get; set; }
        public string event_type_name { get; set; }
        public DateTime reservation_start_dt { get; set; }
        public DateTime reservation_end_dt { get; set; }
    }

    public class EventsResponse
    {
        public Events events { get; set; }
    }

    public class Events
    {
        [JsonProperty("event")]
        public Event _event { get; set; }
    }

    public class Event
    {
        public List<Role> role { get; set; }
        public int event_id { get; set; }
    }

    public class Role
    {
        public string role_name { get; set; }
        public Contact contact { get; set; }
    }

    public class Contact
    {
        public string contact_middle_name { get; set; }
        public string contact_name { get; set; }
        public string contact_last_name { get; set; }
        public string contact_first_name { get; set; }
        public string email { get; set; }
    }

    public class CurrentMeeting : Meeting
    {
        public string OrganizerName { get; set; }
        public string OrganizerEmail { get; set; }

        public CurrentMeeting(Meeting meeting) : base (meeting)
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
        public string Type {
            get
            {
                return _type;
            }
            set
            {
                if (value == "LEC")
                {
                    _type = "Lecture";
                }
                else if (value == "DIS")
                {
                    _type = "Discussion";
                }
                else
                {
                    _type = value;
                }
            }
        }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool MeetingActive
        {
            get
            {
                bool testStart = Start - TimeSpan.FromMinutes(10) <= DateTime.Now;
                bool testEnd = End >= DateTime.Now;
                bool isMaintenance = Type.ToLower() == "maintenance";
                return testStart && testEnd && !isMaintenance;
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
                double totalMinutes;
                if (Start <= DateTime.Now)
                {
                    totalMinutes = End.Subtract(DateTime.Now).TotalMinutes;
                }
                else
                {
                    totalMinutes = End.Subtract(Start).TotalMinutes;
                }
                if (totalMinutes >= 0)
                    return (ushort)Math.Round(totalMinutes);
                else
                    return 0;
            }
        }

        public string TimeRemainingString
        {
            get
            {
                var hourTag = "";
                var minTag = "";
                double hours = TimeRemainingInMin / 60;
                double minutes = TimeRemainingInMin % 60;
                if (hours > 1) { hourTag = "Hours"; }
                else if (hours == 1) { hourTag = "Hour"; }
                if (minutes == 1) { minTag = "Minute"; }
                else { minTag = "Minutes"; }

                if (hourTag.Length == 0) { return string.Format("{0} {1}", minutes, minTag); }
                else { return string.Format("{0} {1} {2} {3}", hours, hourTag, minutes, minTag); }
            }
        }
    }

    public class SpaceResponse
    {
        [JsonProperty("spaces")]
        public Spaces Spaces { get; set; }
    }

    public class Spaces
    {
        [JsonProperty("space")]
        public Space Space { get; set; }
    }

    public class Space
    {
        [JsonProperty("instructions")]
        public string Instructions { get; set; }
        [JsonProperty("space_name")]
        public string SpaceName { get; set; }
        [JsonProperty("feature")]
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("feature_name")]
        public string Name {get; set;}
        [JsonProperty("quantity")]
        public int Quantity { get; set; }
    }


    public class CollegeNetPropertiesConfig
    {
        public string username { get; set; }
        public string password { get; set; }
        public string spaceId { get; set; }
    }

    public class CollegeNetJoinMap : JoinMapBaseAdvanced
    {
        #region Digital
        [JoinName("Refresh Reservations")]
        public JoinDataComplete RefreshReservations = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Refresh Reservations for Today",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });
        [JoinName("Refresh Space Info")]
        public JoinDataComplete RefreshSpaceInfo = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Refresh Space Information",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });
        [JoinName("CurrentMeetingActive")]
        public JoinDataComplete CurrentMeetingActive = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Active",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ScheduleOnline")]
        public JoinDataComplete ScheduleOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Schedule Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("MeetingActive")]
        public JoinDataComplete MeetingActive = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 51,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Active",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });
        #endregion


        #region Analog
        [JoinName("CurrentMeetingTimeRemaining")]
        public JoinDataComplete CurrentMeetingTimeRemaining = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Time Remaining in Minutes",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("FeatureCount")]
        public JoinDataComplete FeatureCount = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Feature Count",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });
        #endregion


        #region Serial

        [JoinName("CurrentMeetingName")]
        public JoinDataComplete CurrentMeetingName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingTitle")]
        public JoinDataComplete CurrentMeetingTitle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingOrganizer")]
        public JoinDataComplete CurrentMeetingOrganizer = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Organizer",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingOrganizerEmail")]
        public JoinDataComplete CurrentMeetingOrganizerEmail = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Organizer Email",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingType")]
        public JoinDataComplete CurrentMeetingType = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingStartTime")]
        public JoinDataComplete CurrentMeetingStartTime = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Start Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("CurrentMeetingEndTime")]
        public JoinDataComplete CurrentMeetingEndTime = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting End Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });
        [JoinName("CurrentMeetingTimeRemainingString")]
        public JoinDataComplete CurrentMeetingTimeRemainingString = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Current Meeting Time Remaining String",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingName")]
        public JoinDataComplete NextMeetingName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Next Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingTitle")]
        public JoinDataComplete NextMeetingTitle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 12,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Next Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingType")]
        public JoinDataComplete NextMeetingType = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 13,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Next Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingStartTime")]
        public JoinDataComplete NextMeetingStartTime = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 14,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Next Meeting Start Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("NextMeetingEndTime")]
        public JoinDataComplete NextMeetingEndTime = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 15,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Next Meeting End Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SpaceName")]
        public JoinDataComplete SpaceName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 21,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Space Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("SpaceInstructions")]
        public JoinDataComplete SpaceInstructions = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 22,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Space Instructions",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingName")]
        public JoinDataComplete MeetingName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 51,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingTitle")]
        public JoinDataComplete MeetingTitle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 101,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Title",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingType")]
        public JoinDataComplete MeetingType = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 151,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Type",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("MeetingTime")]
        public JoinDataComplete MeetingTime = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 201,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Time",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Features")]
        public JoinDataComplete Features = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 251,
                JoinSpan = 50
            },
            new JoinMetadata()
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