using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXml.Serialization;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace DynFusion
{
    public class DynFusionSchedule : EssentialsBridgeableDevice
    {
        public bool fusionOnline = false;
        public event EventHandler<EventArgs> ScheduleChanged;
        public event EventHandler<EventArgs> CurrentMeetingChanged;
        public event EventHandler<EventArgs> NextMeetingChanged;
        public event EventHandler<EventArgs> MeetingInProgressChanged;

        private DynFusionDevice _DynFusion;
        private CTimer getScheduleTimeOut;
        private CTimer getScheduleTimer;
        private CTimer updateCurrentMeeting;
        private SchedulingConfig _Config;
        private ScheduleResponse _scheduleResponse;
        private ushort nextMeetingIndex;

        private uint scheduleFailCount;

        private List<ScheduleResponse> RoomAvailabilityScheduleResponse = new List<ScheduleResponse>();

        private BoolWithFeedback RegisteredForPush = new BoolWithFeedback();
        private BoolWithFeedback ScheduleBusy = new BoolWithFeedback();
        private BoolWithFeedback ScheduleOnline = new BoolWithFeedback();

        private Event _currentMeeting;

        public Event CurrentMeeting
        {
            get { return _currentMeeting; }
            private set
            {
                _currentMeeting = value;
                if (CurrentMeetingChanged != null)
                {
                    CurrentMeetingChanged(this, new EventArgs());
                }

                if (MeetingInProgressChanged != null)
                {
                    Debug.Console(1, this, string.Format("Meeting In Progress Firing Event!"));
                    MeetingInProgressChanged(this, new EventArgs());
                }
            }
        }

        private Event _nextMeeting;

        public Event NextMeeting
        {
            get { return _nextMeeting; }
            private set
            {
                _nextMeeting = value;
                if (NextMeetingChanged != null)
                {
                    NextMeetingChanged(this, new EventArgs());
                }
            }
        }

        public DynFusionSchedule(string key, string name, SchedulingConfig config)
            : base(key, name)
        {
            try
            {
                _Config = config;
            }
            catch (Exception e)
            {
                Debug.Console(0, this, string.Format("Get Schedule Error: {0}", e.Message));
                Debug.ConsoleWithLog(0, this, e.ToString());
            }
        }

        public override bool CustomActivate()
        {
            if (_Config.DynFusionKey != null)
            {
                _DynFusion = (DynFusionDevice)DeviceManager.GetDeviceForKey(_Config.DynFusionKey);
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "DynFusionDeviceKey is not present in config file");
                return false;
            }

            if (_DynFusion == null)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Error getting DynFusionDevice for key {0}",
                    _Config.DynFusionKey);
                return false;
            }

            _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.Use();
            _DynFusion.FusionSymbol.OnlineStatusChange += new OnlineStatusChangeEventHandler(FusionSymbolStatusChange);
            _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.DeviceExtenderSigChange +=
                new DeviceExtenderJoinChangeEventHandler(FusionScheduleExtenderSigChange);
            _DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.DeviceExtenderSigChange +=
                new DeviceExtenderJoinChangeEventHandler(FusionRoomDataExtenderSigChange);
            _DynFusion.RoomInformationUpdated += new EventHandler<EventArgs>(_DynFusion_RoomInformationUpdated);

            ScheduleOnline.value = false;
            getScheduleTimeOut = new CTimer(getScheduleTimeOutCallback, Crestron.SimplSharp.Timeout.Infinite);
            getScheduleTimer = new CTimer(GetRoomSchedule, Crestron.SimplSharp.Timeout.Infinite);
            updateCurrentMeeting = new CTimer(UpdateCurrentMeetingCallback, Crestron.SimplSharp.Timeout.Infinite);
            return true;
        }

        private void _DynFusion_RoomInformationUpdated(object sender, EventArgs e)
        {
            GetRoomSchedule(null);
        }

        private void FusionSymbolStatusChange(object o, OnlineOfflineEventArgs e)
        {
            Debug.Console(1, this, "FusionSymbolStatusChange {0}", e.DeviceOnLine);
            fusionOnline = e.DeviceOnLine;
            if (fusionOnline)
            {
                GetPushSchedule();
            }
        }

        private void GetPushSchedule()
        {
            try
            {
                if (fusionOnline)
                {
                    string requestID = "InitialPushRequest";
                    string fusionActionRequest = "";
                    Debug.Console(1, this, "Sending Push Schedule Reqeust");

                    fusionActionRequest = string.Format("<RequestAction>\n<RequestID>{0}</RequestID>\n" +
                                                        "<ActionID>RegisterPushModel</ActionID>\n" +
                                                        "<Parameters>\n" +
                                                        "<Parameter ID=\"Enabled\" Value=\"1\" />\n" +
                                                        "<Parameter ID=\"RequestID\" Value=\"PushNotification\" />\n" +
                                                        "<Parameter ID=\"Start\" Value=\"00:00:00\" />\n" +
                                                        "<Parameter ID=\"HourSpan\" Value=\"24\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"MeetingID\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"RVMeetingID\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"InstanceID\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"dtStart\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"dtEnd\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"Subject\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"Organizer\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"IsEvent\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"IsPrivate\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"IsExchangePrivate\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"LiveMeeting\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"ShareDocPath\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"PhoneNo\" />\n" +
                                                        "<Parameter ID=\"Field\" Value=\"ParticipantCode\" />\n" +
                                                        "</Parameters>\n" +
                                                        "</RequestAction>\n", requestID);

                    _DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQuery.StringValue =
                        fusionActionRequest;
                }
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, this, string.Format("Get Push Schedule Error: {0}", e.Message), 3);
            }
        }

        private void UpdateCurrentMeetingCallback(object unused)
        {
            Debug.Console(1, this, "Checking for current meeting");
            Event _currentMeetingTemp = null;
            Event _nextMeetingTemp = null;
            uint count = 0;
            while (ScheduleBusy.value && count < 10)
            {
                CrestronEnvironment.Sleep(1000);
                count++;
            }

            if (_scheduleResponse.Events != null && _scheduleResponse.Events.Count > 0)
            {
                //Recheck every minute for current meeting
                updateCurrentMeeting.Reset(60000);

                ushort meetingIndex = 0;
                foreach (Event e in _scheduleResponse.Events)
                {
                    //Check for current meeting
                    //Current meeting is valid if meeting starts in 20 minutes or is currently active
                    if (DateTime.Now >= (e.dtStart - TimeSpan.FromMinutes(20)) && DateTime.Now <= e.dtEnd &&
                        (_currentMeetingTemp == null || _currentMeetingTemp.dtStart > e.dtStart))
                    {
                        _currentMeetingTemp = e;
                    }
                    //If not the current meeting, make the next meeting if it occurs in the future and isn't later than the current "next meeting"
                    else if (DateTime.Now < e.dtStart &&
                             (_nextMeetingTemp == null || _nextMeetingTemp.dtStart > e.dtStart))
                    {
                        _nextMeetingTemp = e;
                        nextMeetingIndex = meetingIndex;
                    }

                    meetingIndex++;
                }

                if (_currentMeetingTemp == null)
                {
                    if (CurrentMeeting != null)
                        CurrentMeeting = null;
                }
                else if (CurrentMeeting == null || (_currentMeetingTemp.MeetingID != CurrentMeeting.MeetingID))
                {
                    CurrentMeeting = _currentMeetingTemp;
                }

                if (_nextMeetingTemp == null)
                {
                    if (NextMeeting != null)
                        NextMeeting = null;
                }
                else if (NextMeeting == null || (_nextMeetingTemp.MeetingID != NextMeeting.MeetingID))
                {
                    NextMeeting = _nextMeetingTemp;
                }
            }
            else
            {
                CurrentMeeting = null;
                NextMeeting = null;
            }
        }

        private void GetRoomSchedule(object unused)
        {
            //If using push model, only update once a day
            if (RegisteredForPush.value)
            {
                DateTime now = DateTime.Now;
                DateTime oneAM = DateTime.Today.AddHours(1);

                if (now >= oneAM)
                {
                    oneAM = oneAM.AddDays(1);
                }

                int timeUntilOneAM = (int)(oneAM - now).TotalMilliseconds;
                getScheduleTimer.Reset(timeUntilOneAM + 60000);
            }
            //If not using push, check every 30 minutes
            else
            {
                getScheduleTimer.Reset(30 * 60000);
            }

            scheduleFailCount = 0;
            GetRoomSchedule();
        }

        public void GetRoomSchedule()
        {
            getScheduleTimeOut.Reset(10000);
            if (ScheduleBusy.value == false && fusionOnline)
            {
                ScheduleBusy.value = true;
                Debug.Console(2, this, string.Format("Get RoomSchedule"));

                try
                {
                    if (_DynFusion.RoomInformation != null)
                    {
                        string roomID = _DynFusion.RoomInformation.ID;
                        string requestType = "ScheduleRequest";
                        string fusionScheduleRequest = "";
                        string RFCTime = string.Format("{0:s}", DateTime.Today);

                        fusionScheduleRequest =
                            string.Format(
                                "<RequestSchedule><RequestID>{0}</RequestID><RoomID>{1}</RoomID><Start>{2}</Start><HourSpan>24</HourSpan></RequestSchedule>",
                                requestType, roomID, RFCTime.ToString());
                        Debug.Console(1, this,
                            string.Format("Get full room schedule request: {0}", fusionScheduleRequest));
                        _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleQuery.StringValue =
                            fusionScheduleRequest;
                    }
                    else
                    {
                        Debug.ConsoleWithLog(0, this, "Error: Room information is null");
                    }
                }
                catch (Exception e)
                {
                    Debug.Console(0, this, string.Format("Get Full Schedule Error: {0}", e.Message));
                    Debug.ConsoleWithLog(2, this, e.ToString());
                }
            }
        }

        public void getScheduleTimeOutCallback(object unused)
        {
            ScheduleBusy.value = false;
            Debug.ConsoleWithLog(0, this, "Error getRoomScheduleTimeOut");

            if (scheduleFailCount < 5)
            {
                scheduleFailCount++;
                GetRoomSchedule();
            }
            else
            {
                ScheduleOnline.value = false;
            }
        }

        private void FusionRoomAttributeExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            Debug.Console(1, this, string.Format("RoomAttributeQuery Response: {0}", args.Sig.StringValue));
        }

        private void FusionRoomDataExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            try
            {
                string result = Regex.Replace(args.Sig.StringValue, "&(?!(amp|apos|quot|lt|gt);)", "&amp;");

                Debug.Console(1, this, string.Format("Args: {0}", result));
                if (args.Sig == _DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQueryResponse &&
                    args.Sig.StringValue != null)
                {
                    XmlDocument actionResponseXML = new XmlDocument();
                    actionResponseXML.LoadXml(result);

                    XmlElement actionResponse = actionResponseXML["ActionResponse"];

                    if (actionResponse != null)
                    {
                        XmlElement requestID = actionResponse["RequestID"];

                        if (requestID.InnerText == "InitialPushRequest")
                        {
                            if (actionResponse["ActionID"].InnerText == "RegisterPushModel")
                            {
                                XmlElement parameters = actionResponse["Parameters"];

                                foreach (XmlElement parameter in parameters)
                                {
                                    if (parameter.HasAttributes)
                                    {
                                        XmlAttributeCollection attributes = parameter.Attributes;

                                        if (attributes["ID"].Value == "Registered")
                                        {
                                            int isRegistered = int.Parse(attributes["Value"].Value.ToString());

                                            if (isRegistered == 1)
                                            {
                                                RegisteredForPush.value = true;
                                                Debug.ConsoleWithLog(0, this,
                                                    string.Format("SchedulePush: {0}", RegisteredForPush.value), 1);
                                            }

                                            else if (isRegistered == 0)
                                            {
                                                RegisteredForPush.value = false;
                                                Debug.ConsoleWithLog(0, this,
                                                    string.Format("SchedulePush: {0}", RegisteredForPush.value), 1);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (requestID.InnerText == "ExtendMeetingRequest")
                        {
                            if (actionResponse["ActionID"].InnerText == "MeetingChange")
                            {
                                this.GetRoomSchedule(null);
                                XmlElement parameters = actionResponse["Parameters"];

                                foreach (XmlElement parameter in parameters)
                                {
                                    if (parameter.HasAttributes)
                                    {
                                        XmlAttributeCollection attributes = parameter.Attributes;

                                        if (attributes["ID"].Value == "MeetingID")
                                        {
                                            if (attributes["Value"].Value != null)
                                            {
                                                string value = attributes["Value"].Value;
                                            }
                                        }
                                        else if (attributes["ID"].Value == "InstanceID")
                                        {
                                            if (attributes["Value"].Value != null)
                                            {
                                                string value = attributes["Value"].Value;
                                            }
                                        }
                                        else if (attributes["ID"].Value == "Status")
                                        {
                                            if (attributes["Value"].Value != null)
                                            {
                                                string value = attributes["Value"].Value;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.CreateResponse)
                {
                    GetRoomSchedule();
                }
                else if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.RemoveMeeting)
                {
                    GetRoomSchedule();
                }
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(2, this, e.ToString());
            }
        }

        private void FusionScheduleExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            try
            {
                Debug.Console(1, this, string.Format("FusionScheduleExtenderSigChange args {0}", args.Sig.StringValue));
                if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleResponse)
                {
                    XmlDocument scheduleXML = new XmlDocument();

                    scheduleXML.LoadXml(args.Sig.StringValue);

                    if (scheduleXML != null)
                    {
                        Debug.Console(1, this, string.Format("Escaped XML {0}", scheduleXML.ToString()));

                        XmlElement response = scheduleXML["ScheduleResponse"];
                        if (response != null)
                        {
                            XmlElement errors = response["Errors"];
                            if (errors != null)
                            {
                                XmlElement error = errors["Error"];
                                if (error != null)
                                {
                                    Debug.Console(0, this, "Schedule request error: {0}", error.InnerText);
                                    return;
                                }
                            }

                            if (response["RequestID"].InnerText == "RVRequest")
                            {
                                XmlElement action = response["Action"];

                                if (action.OuterXml.IndexOf("RequestSchedule") > -1)
                                {
                                    GetRoomSchedule();
                                }
                            }

                            #region ScheduleRequest

                            else if (response["RequestID"].InnerText == "ScheduleRequest")
                            {
                                Debug.Console(1, this, string.Format("ScheduleResponse start"));

                                _scheduleResponse = new ScheduleResponse();
                                _scheduleResponse.RoomName =
                                    scheduleXML.FirstChild.SelectSingleNode("RoomName").InnerText;
                                _scheduleResponse.RequestID =
                                    scheduleXML.FirstChild.SelectSingleNode("RequestID").InnerText;
                                _scheduleResponse.RoomID = scheduleXML.FirstChild.SelectSingleNode("RoomID").InnerText;
                                Debug.Console(1, this, string.Format("EventStack Count start"));
                                XmlNodeList eventStack = scheduleXML.FirstChild.SelectNodes("Event");
                                Debug.Console(1, this, string.Format("EventStack Count: {0}", eventStack.Count));
                                if (eventStack.Count > 0)
                                {
                                    for (ushort i = 0; i < eventStack.Count; i++)
                                    {
                                        try
                                        {
                                            Debug.Console(1, this,
                                                string.Format("Deserializing: {0}", eventStack.Item(i).OuterXml));
                                            Event newEvent =
                                                CrestronXMLSerialization.DeSerializeObject<Event>(
                                                    new XmlReader(eventStack.Item(i).OuterXml));

                                            if (newEvent.dtStart.Date > DateTime.Today ||
                                                newEvent.dtEnd.Date < DateTime.Today)
                                            {
                                                continue;
                                            }

                                            _scheduleResponse.Events.Add(newEvent);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Console(0, this,
                                                string.Format("Exception deserializing xml for event {0}: {1}", i,
                                                    ex.Message));
                                        }
                                    }

                                    Debug.Console(1, this, "Deserializing xml complete");
                                }

                                getScheduleTimeOut.Stop();
                                ScheduleOnline.value = true;
                                ScheduleBusy.value = false;
                                CrestronInvoke.BeginInvoke((o) => { UpdateCurrentMeetingCallback(null); });

                                if (ScheduleChanged != null)
                                {
                                    Debug.Console(0, this, string.Format("Schedule Changed Firing Event!"));
                                    ScheduleChanged(this, new EventArgs());
                                }
                            }

                            #endregion

                            else if (response["RequestID"].InnerText == "PushNotification")
                            {
                                this.GetRoomSchedule(null);
                                Debug.Console(1, this, string.Format("Got a Push Notification!"));
                            }

                            #region RoomListScheduleRequest

                            else if (response["RequestID"].InnerText == "AvailableRoomSchedule")
                            {
                                XmlNode responseEvent = response.SelectSingleNode("Event");
                                if (responseEvent != null)
                                {
                                    RoomAvailabilityScheduleResponse = null;

                                    foreach (XmlElement element in scheduleXML.FirstChild.ChildNodes)
                                    {
                                        ScheduleResponse AvailibleSchedule = new ScheduleResponse();

                                        if (element.Name == "RequestID")
                                        {
                                            AvailibleSchedule.RequestID = element.InnerText;
                                        }
                                        else if (element.Name == "RoomID")
                                        {
                                            AvailibleSchedule.RoomID = element.InnerText;
                                        }
                                        else if (element.Name == "RoomName")
                                        {
                                            AvailibleSchedule.RoomName = element.InnerText;
                                        }
                                        else if (element.Name == "Event")
                                        {
                                            XmlReader readerXML = new XmlReader(element.OuterXml);

                                            Event RoomAvailabilityScheduleEvent = new Event();

                                            RoomAvailabilityScheduleEvent =
                                                CrestronXMLSerialization.DeSerializeObject<Event>(readerXML);

                                            AvailibleSchedule.Events.Add(RoomAvailabilityScheduleEvent);
                                        }

                                        RoomAvailabilityScheduleResponse.Add(AvailibleSchedule);
                                    }
                                }
                            }

                            #endregion
                        }
                    }
                }

                if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.CreateResponse)
                {
                    GetRoomSchedule();
                }
                else if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.RemoveMeeting)
                {
                    GetRoomSchedule();
                }
            }

            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, this, "{0}\n{1}\n{2}", e.InnerException, e.Message, e.StackTrace);
            }
        }

        public override void LinkToApi(Crestron.SimplSharpPro.DeviceSupport.BasicTriList trilist, uint joinStart,
            string joinMapKey, PepperDash.Essentials.Core.Bridges.EiscApiAdvanced bridge)
        {
            try
            {
                SchedulingJoinMap joinMap = new SchedulingJoinMap(joinStart);
                ScheduleOnline.Feedback.LinkInputSig(trilist.BooleanInput[joinMap.ScheduleOnline.JoinNumber]);
                trilist.SetSigTrueAction(joinMap.GetSchedule.JoinNumber, () => GetRoomSchedule());
                RegisteredForPush.Feedback.LinkInputSig(
                    trilist.BooleanInput[joinMap.PushNotificationRegistered.JoinNumber]);


                _DynFusion.RoomInformationUpdated += ((s, e) =>
                {
                    trilist.StringInput[joinMap.RoomID.JoinNumber].StringValue = _DynFusion.RoomInformation.ID;
                    trilist.StringInput[joinMap.RoomLocation.JoinNumber].StringValue =
                        _DynFusion.RoomInformation.Location;
                });

                MeetingInProgressChanged += ((s, e) =>
                {
                    ushort meetingCount = 0;
                    if (_scheduleResponse != null)
                    {
                        foreach (Event meeting in _scheduleResponse.Events)
                        {
                            trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + meetingCount].BoolValue =
                                meeting.isInProgress;
                            meetingCount++;
                        }
                    }

                    for (ushort i = meetingCount; i < 20; i++)
                    {
                        trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + meetingCount].BoolValue = false;
                    }
                });

                CurrentMeetingChanged += ((s, e) =>
                {
                    try
                    {
                        Debug.Console(1, this, "CurrentMeetingChanged");
                        if (CurrentMeeting != null)
                        {
                            if (CurrentMeeting.Organizer.Length > 0)
                            {
                                trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue =
                                    CurrentMeeting.Organizer;
                            }
                            else
                            {
                                trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue =
                                    CurrentMeeting.OrganizerSMTP;
                            }

                            trilist.StringInput[joinMap.CurrentMeetingSubject.JoinNumber].StringValue =
                                CurrentMeeting.Subject;
                            trilist.StringInput[joinMap.CurrentMeetingMeetingID.JoinNumber].StringValue =
                                CurrentMeeting.MeetingID;
                            trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue =
                                CurrentMeeting.StartTime;
                            trilist.StringInput[joinMap.CurrentMeetingStartDate.JoinNumber].StringValue =
                                CurrentMeeting.StartDate;
                            trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue =
                                CurrentMeeting.EndTime;
                            trilist.StringInput[joinMap.CurrentMeetingEndDate.JoinNumber].StringValue =
                                CurrentMeeting.EndDate;
                            trilist.StringInput[joinMap.CurrentMeetingDuration.JoinNumber].StringValue =
                                CurrentMeeting.DurationInMinutes;
                            trilist.BooleanInput[joinMap.CurrentMeetingInProgress.JoinNumber].BoolValue =
                                CurrentMeeting.isInProgress;
                            trilist.StringInput[joinMap.CurrentMeetingOrganizerSMTP.JoinNumber].StringValue =
                                CurrentMeeting.OrganizerSMTP;
                        }
                        else
                        {
                            trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingSubject.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingMeetingID.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingStartDate.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingEndDate.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingDuration.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.CurrentMeetingOrganizerSMTP.JoinNumber].StringValue = "";
                            trilist.BooleanInput[joinMap.CurrentMeetingInProgress.JoinNumber].BoolValue = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, Debug.ErrorLogLevel.Error, ex.Message);
                    }
                });
                NextMeetingChanged += ((s, e) =>
                {
                    try
                    {
                        Debug.Console(1, this, "NextMeetingChanged");
                        if (NextMeeting != null)
                        {
                            if (NextMeeting.Organizer.Length > 0)
                            {
                                trilist.StringInput[joinMap.NextMeetingOrganizer.JoinNumber].StringValue =
                                    NextMeeting.Organizer;
                            }
                            else
                            {
                                trilist.StringInput[joinMap.NextMeetingOrganizer.JoinNumber].StringValue =
                                    NextMeeting.OrganizerSMTP;
                            }

                            trilist.StringInput[joinMap.NextMeetingOrganizer.JoinNumber].StringValue =
                                NextMeeting.Organizer;
                            trilist.StringInput[joinMap.NextMeetingSubject.JoinNumber].StringValue =
                                NextMeeting.Subject;
                            trilist.StringInput[joinMap.NextMeetingMeetingID.JoinNumber].StringValue =
                                NextMeeting.MeetingID;
                            trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue =
                                NextMeeting.StartTime;
                            trilist.StringInput[joinMap.NextMeetingStartDate.JoinNumber].StringValue =
                                NextMeeting.StartDate;
                            trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue =
                                NextMeeting.EndTime;
                            trilist.StringInput[joinMap.NextMeetingEndDate.JoinNumber].StringValue =
                                NextMeeting.EndDate;
                            trilist.StringInput[joinMap.NextMeetingDuration.JoinNumber].StringValue =
                                NextMeeting.DurationInMinutes;
                            trilist.UShortInput[joinMap.NextMeetingIndex.JoinNumber].UShortValue =
                                (ushort)(nextMeetingIndex + 1);
                        }
                        else
                        {
                            trilist.StringInput[joinMap.NextMeetingOrganizer.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingSubject.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingMeetingID.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingStartDate.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingEndDate.JoinNumber].StringValue = "";
                            trilist.StringInput[joinMap.NextMeetingDuration.JoinNumber].StringValue = "";
                            trilist.UShortInput[joinMap.NextMeetingIndex.JoinNumber].UShortValue = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, Debug.ErrorLogLevel.Error, ex.Message);
                    }
                });

                ScheduleChanged += ((s, e) =>
                {
                    try
                    {
                        Debug.Console(1, this, "ScheduleChanged");
                        ushort meetingCount = 0;
                        if (_scheduleResponse != null)
                        {
                            foreach (Event meeting in _scheduleResponse.Events)
                            {
                                trilist.StringInput[joinMap.MeetingSubject.JoinNumber + meetingCount].StringValue =
                                    meeting.Subject;
                                if (meeting.Organizer.Length > 0)
                                {
                                    trilist.StringInput[joinMap.MeetingOrganizer.JoinNumber + meetingCount]
                                        .StringValue = meeting.Organizer;
                                }
                                else
                                {
                                    trilist.StringInput[joinMap.MeetingOrganizer.JoinNumber + meetingCount]
                                        .StringValue = meeting.OrganizerSMTP;
                                }

                                trilist.StringInput[joinMap.MeetingTime.JoinNumber + meetingCount].StringValue =
                                    meeting.StartTime + " - " + meeting.EndTime;
                                trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + meetingCount].BoolValue =
                                    meeting.isInProgress;
                                meetingCount++;
                            }
                        }

                        for (ushort i = meetingCount; i < 20; i++)
                        {
                            trilist.StringInput[joinMap.MeetingSubject.JoinNumber + i].StringValue = "";
                            trilist.StringInput[joinMap.MeetingOrganizer.JoinNumber + i].StringValue = "";
                            trilist.StringInput[joinMap.MeetingTime.JoinNumber + i].StringValue = "";
                            trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + i].BoolValue = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, Debug.ErrorLogLevel.Error, ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, Debug.ErrorLogLevel.Error, ex.Message);
            }
        }
    }

    //************************************************************************************************************************************
    // Helper Classes
    public class LocalTimeRequest
    {
        public string RequestID { get; set; }
    }

    public class RequestSchedule
    {
        public string RequestID { get; set; }
        public string RoomID { get; set; }
        public DateTime Start { get; set; }
        public double HourSpan { get; set; }

        public RequestSchedule(string requestID, string roomID)
        {
            RequestID = requestID;
            RoomID = roomID;
            Start = DateTime.Now;
            HourSpan = 24;
        }
    }

    public class RequestAction
    {
        public string RequestID { get; set; }
        public string RoomID { get; set; }
        public string ActionID { get; set; }
        public List<Parameter> Parameters { get; set; }

        public RequestAction(string roomID, string actionID, List<Parameter> parameters)
        {
            RoomID = roomID;
            ActionID = actionID;
            Parameters = parameters;
        }
    }

    public class ActionResponse
    {
        public string RequsetID { get; set; }
        public string ActionID { get; set; }
        public List<Parameter> Parameters { get; set; }
    }

    public class Parameter
    {
        public string ID { get; set; }
        public string Value { get; set; }
    }

    public class ScheduleResponse
    {
        public string RequestID { get; set; }
        public string RoomID { get; set; }
        public string RoomName { get; set; }
        public List<Event> Events { get; set; }

        public ScheduleResponse()
        {
            Events = new List<Event>();
        }
    }

    public class Event
    {
        public string Recurring { get; set; }
        public string MeetingID { get; set; }
        public string RVMeetingID { get; set; }
        public DateTime dtStart { get; set; }
        public DateTime dtEnd { get; set; }
        public string Organizer { get; set; }
        public string OrganizerSMTP { get; set; }
        public string Subject { get; set; }
        public string IsPrivate { get; set; }
        public string IsExchangePrivate { get; set; }
        public Attendees Attendees { get; set; }
        public string IsEvent { get; set; }
        public string IsRoomViewMeeting { get; set; }
        public MeetingTypes MeetingTypes { get; set; }
        public LiveMeeting LiveMeeting { get; set; }
        public string WelcomeMsg { get; set; }
        public string Body { get; set; }
        public string Location { get; set; }
        public string ShareDocPath { get; set; }
        public string ParticipantCode { get; set; }
        public string PhoneNo { get; set; }
        public string InstanceID { get; set; }

        public Event()
        {
        }

        public string StartTime
        {
            get
            {
                string startTimeShort;

                startTimeShort = dtStart.ToShortTimeString();

                return startTimeShort;
            }
        }

        public string StartDate
        {
            get
            {
                string startDateShort;

                startDateShort = dtStart.ToShortDateString();

                return startDateShort;
            }
        }

        public string EndTime
        {
            get
            {
                string endTimeShort;

                endTimeShort = dtEnd.ToShortTimeString();

                return endTimeShort;
            }
        }

        public string EndDate
        {
            get
            {
                string endDateShort;

                endDateShort = dtEnd.ToShortDateString();

                return endDateShort;
            }
        }

        public string DurationInMinutes
        {
            get
            {
                string duration;

                TimeSpan timeSpan = dtEnd.Subtract(dtStart);
                int hours = timeSpan.Hours;
                double minutes = timeSpan.Minutes;
                double minutesRounded = Math.Round(minutes);
                if (hours > 0)
                {
                    duration = string.Format("{0} Hours {1} Minutes", hours, minutesRounded);
                }
                else
                {
                    duration = string.Format("{0} Minutes", minutesRounded);
                }

                return duration;
            }
        }

        public double TimeRemainingInMin
        {
            get
            {
                DateTime timeMarker = new DateTime();
                if (dtStart <= DateTime.Now)
                {
                    timeMarker = dtEnd;
                }
                else
                {
                    timeMarker = dtStart;
                }

                double totalMinutes = timeMarker.Subtract(DateTime.Now).TotalMinutes;
                if (totalMinutes >= 0)
                    return Math.Round(totalMinutes);
                else
                    return 0;
            }
        }

        public string TimeRemainingString
        {
            get
            {
                DateTime now = DateTime.Now;
                string remainingTimeString;

                DateTime timeMarker = new DateTime();
                if (GetInProgress())
                {
                    timeMarker = dtEnd;
                }
                else
                {
                    timeMarker = dtStart;
                }

                string hourTag = "";
                string minTag = "";
                int hours = timeMarker.Subtract(DateTime.Now).Hours;
                int minutes = timeMarker.Subtract(DateTime.Now).Minutes;
                if (hours > 1)
                {
                    hourTag = "Hours";
                }
                else if (hours == 1)
                {
                    hourTag = "Hour";
                }

                if (minutes == 1)
                {
                    minTag = "Minute";
                }
                else
                {
                    minTag = "Minutes";
                }

                if (hourTag.Length == 0)
                {
                    remainingTimeString = string.Format("{0} {1}", minutes, minTag);
                }
                else
                {
                    remainingTimeString = string.Format("{0} {1} {2} {3}", hours, hourTag, minutes, minTag);
                }

                return remainingTimeString;
            }
        }

        public bool isInProgress
        {
            get { return GetInProgress(); }
        }

        private bool GetInProgress()
        {
            DateTime now = DateTime.Now;

            if (now >= dtStart && now <= dtEnd)
            {
                Debug.Console(0, "Meeting in progress {0}", this.Subject);
                return true;
            }
            else
            {
                Debug.Console(0, "Meeting not in progress {0}", this.Subject);
                return false;
            }
        }
    }

    public class Attendees
    {
        public Required Required { get; set; }
        public Optional Optional { get; set; }
    }

    public class Required
    {
        public List<string> Attendee { get; set; }
    }

    public class Optional
    {
        public List<string> Attendee { get; set; }
    }


    public class MeetingType
    {
        public string ID { get; set; }
        public string Value { get; set; }
    }

    public class MeetingTypes
    {
        public List<MeetingType> MeetingType { get; set; }
    }

    public class LiveMeeting
    {
        public string URL { get; set; }
        public string ID { get; set; }
        public string Key { get; set; }
        public string Subject { get; set; }
    }

    public class LiveMeetingURL
    {
        public LiveMeeting LiveMeeting { get; set; }
    }
}