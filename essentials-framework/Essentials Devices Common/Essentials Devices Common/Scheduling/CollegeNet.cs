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
        public List<Meeting> Meetings { get; private set; }
        CTimer updateCurrentMeeting;

        private CurrentMeeting _currentMeeting;
        public CurrentMeeting CurrentMeeting
        {
            get { return _currentMeeting; }
            private set
            {
                _nextMeeting = value;
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
            updateCurrentMeeting = new CTimer(UpdateCurrentMeetingCallback, Crestron.SimplSharp.Timeout.Infinite);
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
            trilist.SetSigTrueAction(joinMap.RefreshReservations.JoinNumber, GetTodaysReservations);


            MeetingsUpdated += (o, a) =>
            {
                uint count = 0;
                if (Meetings != null)
                {
                    foreach (var meeting in Meetings)
                    {
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
                    trilist.StringInput[joinMap.MeetingTitle.JoinNumber + count].StringValue = "";
                    trilist.StringInput[joinMap.MeetingName.JoinNumber + count].StringValue = "";
                    trilist.StringInput[joinMap.MeetingType.JoinNumber + count].StringValue = "";
                    trilist.StringInput[joinMap.MeetingTime.JoinNumber + count].StringValue = "";
                }
            };

            CurrentMeetingUpdated += (o, a) =>
            {

            };

            NextMeetingUpdated += (o, a) =>
            {

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

        public void GetTodaysReservations()
        {
            if (spaceId != null)
            {
                GetData(string.Format("reservations.json?space_id={0}", spaceId), "Reservations");
            }
        }

        public void GetSpaceInfo()
        {
            if (spaceId != null)
            {
                GetData(string.Format("space/detail/spdetail.json?space_id={0}", spaceId), "Space");
            }
        }

        void UpdateCurrentMeetingCallback(object unused)
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
                    if (DateTime.Now >= (m.Start - TimeSpan.FromMinutes(20)) && DateTime.Now <= m.End && (_currentMeetingTemp == null || _currentMeetingTemp.Start > m.Start))
                    {
                        _currentMeetingTemp = m;
                    }
                    //If not the current meeting, make the next meeting if it occurs in the future and isn't later than the current "next meeting"
                    else if (DateTime.Now < m.Start && (_nextMeetingTemp == null || _nextMeetingTemp.Start > m.Start))
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
                    foreach (Reservation reservation in response.reservations.reservation)
                    {
                        try
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

        public CurrentMeeting(Meeting meeting)
        {
            Id = meeting.Id;
            Title = meeting.Title;
            Name = meeting.Name;
            Type = meeting.Type;
            Start = meeting.Start;
            End = meeting.End;
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
        #endregion


        #region Analog
        #endregion


        #region Serial
        [JoinName("MeetingTitle")]
        public JoinDataComplete MeetingTitle = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Title",
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

        [JoinName("MeetingType")]
        public JoinDataComplete MeetingType = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 101,
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
                JoinNumber = 151,
                JoinSpan = 50
            },
            new JoinMetadata()
            {
                Description = "Meeting Time",
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