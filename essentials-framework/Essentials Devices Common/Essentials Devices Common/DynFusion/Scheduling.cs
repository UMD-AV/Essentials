using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXml.Serialization;
using Crestron.SimplSharp.CrestronXmlLinq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Fusion;
using System.Threading;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharp.Net;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace DynFusion
{
	public class DynFusionScheduleChangeEventArgs : EventArgs
	{
		string data;
		public DynFusionScheduleChangeEventArgs(string someString)
		{
			data = someString;
		}

	}
	public class DynFusionSchedule : EssentialsBridgeableDevice	
	{
		public bool fusionOnline = false;
		public event EventHandler<DynFusionScheduleChangeEventArgs> ScheduleChanged;

		DynFusionDevice _DynFusion;
		CTimer getScheduleTimeOut;
		SchedulingConfig _Config;
        ScheduleResponse _scheduleResponse;

		List<ScheduleResponse> RoomAvailabilityScheduleResponse = new List<ScheduleResponse>();

		private BoolWithFeedback RegisteredForPush = new BoolWithFeedback();
		private BoolWithFeedback ScheduleBusy = new BoolWithFeedback();		
		public Event CurrentMeeting;
		public Event NextMeeting;

		public DynFusionSchedule(string key, string name, SchedulingConfig config)
			: base(key, name)
		{
			try
			{
				_Config = config;
			}
			catch (Exception e)
			{
				Debug.Console(0, this, String.Format("Get Schedule Error: {0}", e.Message));
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
				Debug.Console(0, Debug.ErrorLogLevel.Error, "Error getting DynFusionDevice for key {0}", _Config.DynFusionKey);
				return false;
			}
			_DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.Use();
			_DynFusion.FusionSymbol.OnlineStatusChange += new OnlineStatusChangeEventHandler(FusionSymbolStatusChange);
			_DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.DeviceExtenderSigChange += new DeviceExtenderJoinChangeEventHandler(FusionScheduleExtenderSigChange);
			_DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.DeviceExtenderSigChange += new DeviceExtenderJoinChangeEventHandler(FusionRoomDataExtenderSigChange);
			return true;
		}

		void FusionSymbolStatusChange(object o, OnlineOfflineEventArgs e)
		{
			Debug.Console(0, this, "FusionSymbolStatusChange {0}", e.DeviceOnLine);
			fusionOnline = e.DeviceOnLine;
			if (fusionOnline)
			{
				GetPushSchedule();
			}
		}

		void GetPushSchedule()
		{
			try
			{
				if (fusionOnline)
				{
					string requestID = "InitialPushRequest";
					string fusionActionRequest = "";

					fusionActionRequest = String.Format("<RequestAction>\n<RequestID>{0}</RequestID>\n" +
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

					_DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQuery.StringValue = fusionActionRequest;
				}
			}
			catch (Exception e)
			{
				Debug.ConsoleWithLog(0, this, String.Format("Get Push Schedule Error: {0}", e.Message), 3);
			}
		}

		void GetRoomSchedule(object unused)
		{
			GetRoomSchedule();
		}

		public void GetRoomSchedule()
		{
            if (ScheduleBusy.value == false && fusionOnline)
			{
				ScheduleBusy.value = true;
				getScheduleTimeOut = new CTimer(getRoomScheduleTimeOut, 6000);
				Debug.Console(2, this, String.Format("Get RoomSchedule"));
				string roomID = _DynFusion.RoomInformation.ID;
				string requestType = "ScheduleRequest";
                try
                {
                    string fusionScheduleRequest = "";
                    string RFCTime = String.Format("{0:s}", DateTime.Today);

                    fusionScheduleRequest = String.Format("<RequestSchedule><RequestID>{0}</RequestID><RoomID>{1}</RoomID><Start>{2}</Start><HourSpan>24</HourSpan></RequestSchedule>", requestType, roomID, RFCTime.ToString());

                    Debug.Console(0, this, String.Format("Get full room schedule request: {0}", fusionScheduleRequest));
                    _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleQuery.StringValue = fusionScheduleRequest;
                }
                catch (Exception e)
                {
                    Debug.Console(0, this, String.Format("Get Full Schedule Error: {0}", e.Message));
                    Debug.ConsoleWithLog(2, this, e.ToString());
                }
			}
		}

		public void getRoomScheduleTimeOut(object unused)
		{
			ScheduleBusy.value = false;
			Debug.ConsoleWithLog(0, this, "Error getRoomScheduleTimeOut");
		}

		void FusionRoomAttributeExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
		{
			Debug.Console(0, this, String.Format("RoomAttributeQuery Response: {0}", args.Sig.StringValue));
		}
		void FusionRoomDataExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
		{
			try
			{
				string result = Regex.Replace(args.Sig.StringValue, "&(?!(amp|apos|quot|lt|gt);)", "&amp;");

				Debug.Console(0, this, String.Format("Args: {0}", result));
				if (args.Sig == _DynFusion.FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQueryResponse && args.Sig.StringValue != null)
				{
					XmlDocument actionResponseXML = new XmlDocument();
					actionResponseXML.LoadXml(result);

					var actionResponse = actionResponseXML["ActionResponse"];

					if (actionResponse != null)
					{
						var requestID = actionResponse["RequestID"];

						if (requestID.InnerText == "InitialPushRequest")
						{
							if (actionResponse["ActionID"].InnerText == "RegisterPushModel")
							{
								var parameters = actionResponse["Parameters"];

								foreach (XmlElement parameter in parameters)
								{
									if (parameter.HasAttributes)
									{
										var attributes = parameter.Attributes;

										if (attributes["ID"].Value == "Registered")
										{
											var isRegsitered = Int32.Parse(attributes["Value"].Value.ToString());

											if (isRegsitered == 1)
											{
												RegisteredForPush.value = true;
												Debug.ConsoleWithLog(0, this, string.Format("SchedulePush: {0}", RegisteredForPush.value), 1);
											}

											else if (isRegsitered == 0)
											{
												RegisteredForPush.value = false;
												Debug.ConsoleWithLog(0, this, string.Format("SchedulePush: {0}", RegisteredForPush.value), 1);
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
								var parameters = actionResponse["Parameters"];

								foreach (XmlElement parameter in parameters)
								{
									if (parameter.HasAttributes)
									{
										var attributes = parameter.Attributes;

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

		void FusionScheduleExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
		{
			try
			{
				Debug.Console(0, this, string.Format("FusionScheduleExtenderSigChange args {0}", args.Sig.StringValue));
				if (args.Sig == _DynFusion.FusionSymbol.ExtenderRoomViewSchedulingDataReservedSigs.ScheduleResponse)
				{
					XmlDocument scheduleXML = new XmlDocument();

                    scheduleXML.LoadXml(args.Sig.StringValue);

					if (scheduleXML != null)
					{

						Debug.Console(0, this, string.Format("Escaped XML {0}", scheduleXML.ToString()));

						var response = scheduleXML["ScheduleResponse"];
						var responseEvent = response.SelectSingleNode("Event");

						if (response != null)
						{
							if (response["RequestID"].InnerText == "RVRequest")
							{
								var action = response["Action"];

								if (action.OuterXml.IndexOf("RequestSchedule") > -1)
								{
									GetRoomSchedule();
								}
							}
							#region ScheduleRequest
							else if (response["RequestID"].InnerText == "ScheduleRequest")
							{
								CurrentMeeting = null;
								NextMeeting = null;
                                Debug.Console(0, this, String.Format("ScheduleResponse start"));

								_scheduleResponse = new ScheduleResponse();
								_scheduleResponse.RoomName = scheduleXML.FirstChild.SelectSingleNode("RoomName").InnerText;
								_scheduleResponse.RequestID = scheduleXML.FirstChild.SelectSingleNode("RequestID").InnerText;
								_scheduleResponse.RoomID = scheduleXML.FirstChild.SelectSingleNode("RoomID").InnerText;
                                Debug.Console(0, this, String.Format("EventStack Count start"));
								var eventStack = scheduleXML.FirstChild.SelectNodes("Event");
								Debug.Console(0, this, String.Format("EventStack Count: {0}", eventStack.Count));
								if (eventStack.Count > 0)
								{
                                    for(ushort i = 0; i < eventStack.Count; i++)
                                    {
                                        try
                                        {
                                            Debug.Console(0, this, String.Format("Deserializing: {0}", eventStack.Item(i).OuterXml));
                                            Event newEvent = CrestronXMLSerialization.DeSerializeObject<Event>(new XmlReader(eventStack.Item(i).OuterXml));

                                            if (newEvent.dtStart.Date > DateTime.Today || newEvent.dtEnd.Date < DateTime.Today)
                                            {
                                                continue;
                                            }
                                            _scheduleResponse.Events.Add(newEvent);

                                            if (DateTime.Now < newEvent.dtEnd)
                                            {
                                                CurrentMeeting = newEvent;
                                            }
                                            if (DateTime.Now < newEvent.dtStart)
                                            {
                                                NextMeeting = newEvent;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.Console(0, this, String.Format("Exception deserializing xml for event {0}: {1}", i, ex.Message));
                                        }
                                    }
                                    Debug.Console(0, this, "Deserializing xml complete");
								}

								getScheduleTimeOut.Stop();
                                if (ScheduleChanged != null)
								{
									Debug.Console(0, this, String.Format("Schedule Changed Firing Event!"));
                                    ScheduleChanged(this, new DynFusionScheduleChangeEventArgs("BAM!"));
								}
								
								ScheduleBusy.value = false;
							}
							#endregion
							else if (response["RequestID"].InnerText == "PushNotification")
							{
								this.GetRoomSchedule(null);
								Debug.Console(0, this, String.Format("Got a Push Notification!"));

							}
							#region RoomListScheduleRequest
							else if (response["RequestID"].InnerText == "AvailableRoomSchedule")
							{
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

											RoomAvailabilityScheduleEvent = CrestronXMLSerialization.DeSerializeObject<Event>(readerXML);

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

		public override void LinkToApi(Crestron.SimplSharpPro.DeviceSupport.BasicTriList trilist, uint joinStart, string joinMapKey, PepperDash.Essentials.Core.Bridges.EiscApiAdvanced bridge)
		{
			try
			{
				var joinMap = new SchedulingJoinMap(joinStart);
				ScheduleBusy.Feedback.LinkInputSig(trilist.BooleanInput[joinMap.ScheduleBusy.JoinNumber]);
				trilist.SetSigTrueAction(joinMap.GetSchedule.JoinNumber, () => GetRoomSchedule());
				RegisteredForPush.Feedback.LinkInputSig(trilist.BooleanInput[joinMap.PushNotificationRegistered.JoinNumber]);

				ScheduleChanged += ((s, e) =>
				{
					try
					{
						Debug.Console(0, this, "ScheduleChanged");
						if (CurrentMeeting != null)
						{
							trilist.StringInput[joinMap.CurrentMeetingOrganizer.JoinNumber].StringValue = CurrentMeeting.Organizer;
							trilist.StringInput[joinMap.CurrentMeetingSubject.JoinNumber].StringValue = CurrentMeeting.Subject;
							trilist.StringInput[joinMap.CurrentMeetingMeetingID.JoinNumber].StringValue = CurrentMeeting.MeetingID;
							trilist.StringInput[joinMap.CurrentMeetingStartTime.JoinNumber].StringValue = CurrentMeeting.StartTime;
							trilist.StringInput[joinMap.CurrentMeetingStartDate.JoinNumber].StringValue = CurrentMeeting.StartDate;
							trilist.StringInput[joinMap.CurrentMeetingEndTime.JoinNumber].StringValue = CurrentMeeting.EndTime;
							trilist.StringInput[joinMap.CurrentMeetingEndDate.JoinNumber].StringValue = CurrentMeeting.EndDate;
							trilist.StringInput[joinMap.CurrentMeetingDuration.JoinNumber].StringValue = CurrentMeeting.DurationInMinutes;
							trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber].BoolValue = CurrentMeeting.isInProgress;
                            trilist.StringInput[joinMap.CurrentMeetingOrganizerSMTP.JoinNumber].StringValue = CurrentMeeting.OrganizerSMTP;
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
                            trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber].BoolValue = false;
						}

						if (NextMeeting != null)
						{
							trilist.StringInput[joinMap.NextMeetingOrganizer.JoinNumber].StringValue = NextMeeting.Organizer;
							trilist.StringInput[joinMap.NextMeetingSubject.JoinNumber].StringValue = NextMeeting.Subject;
							trilist.StringInput[joinMap.NextMeetingMeetingID.JoinNumber].StringValue = NextMeeting.MeetingID;
							trilist.StringInput[joinMap.NextMeetingStartTime.JoinNumber].StringValue = NextMeeting.StartTime;
							trilist.StringInput[joinMap.NextMeetingStartDate.JoinNumber].StringValue = NextMeeting.StartDate;
							trilist.StringInput[joinMap.NextMeetingEndTime.JoinNumber].StringValue = NextMeeting.EndTime;
							trilist.StringInput[joinMap.NextMeetingEndDate.JoinNumber].StringValue = NextMeeting.EndDate;
							trilist.StringInput[joinMap.NextMeetingDuration.JoinNumber].StringValue = NextMeeting.DurationInMinutes;
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
						}
                        ushort meetingCount = 0;
                        if (_scheduleResponse != null)
                        {
                            foreach (var meeting in _scheduleResponse.Events)
                            {
                                trilist.StringInput[joinMap.MeetingSubject.JoinNumber + meetingCount].StringValue = meeting.Subject;
                                trilist.StringInput[joinMap.MeetingOrganizer.JoinNumber + meetingCount].StringValue = meeting.Organizer;
                                trilist.StringInput[joinMap.MeetingTime.JoinNumber + meetingCount].StringValue = meeting.StartTime + " - " + meeting.EndTime;
                                trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + meetingCount].BoolValue = meeting.isInProgress;
                                meetingCount++;
                            }
                        }
                        for (ushort i = meetingCount; i < 20; i++)
                        {
                            trilist.StringInput[joinMap.MeetingSubject.JoinNumber + meetingCount].StringValue = "";
                            trilist.StringInput[joinMap.MeetingOrganizer.JoinNumber + meetingCount].StringValue = "";
                            trilist.StringInput[joinMap.MeetingTime.JoinNumber + meetingCount].StringValue = "";
                            trilist.BooleanInput[joinMap.MeetingInProgress.JoinNumber + meetingCount].BoolValue = false;
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

				var timeSpan = dtEnd.Subtract(dtStart);
				int hours = timeSpan.Hours;
				double minutes = timeSpan.Minutes;
				double minutesRounded = Math.Round(minutes);
				if (hours > 0)
				{
					duration = String.Format("{0} Hours {1} Minutes", hours, minutesRounded);
				}
				else
				{
					duration = String.Format("{0} Minutes", minutesRounded);
				}

				return duration;
			}
		}

		public double TimeRemainingInMin
		{
			get
			{
				DateTime timeMarker = new DateTime();
				if (dtStart <= DateTime.Now) { timeMarker = dtEnd; }
				else { timeMarker = dtStart; }

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
				var now = DateTime.Now;
				string remainingTimeString;

				DateTime timeMarker = new DateTime();
				if (GetInProgress()) { timeMarker = dtEnd; }
				else { timeMarker = dtStart; }

				var hourTag = "";
				var minTag = "";
				int hours = timeMarker.Subtract(DateTime.Now).Hours;
				int minutes = timeMarker.Subtract(DateTime.Now).Minutes;
				if (hours > 1) { hourTag = "Hours"; }
				else if (hours == 1) { hourTag = "Hour"; }
				if (minutes == 1) { minTag = "Minute"; }
				else { minTag = "Minutes"; }

				if (hourTag.Length == 0) { remainingTimeString = string.Format("{0} {1}", minutes, minTag); }
				else { remainingTimeString = string.Format("{0} {1} {2} {3}", hours, hourTag, minutes, minTag); }

				return remainingTimeString;

			}
		}

		public bool isInProgress
		{
			get
			{
				return GetInProgress();
			}
		}

		bool GetInProgress()
		{
			var now = DateTime.Now;
 
			if (now > dtStart && now < dtEnd)
			{
				return true;
			}
			else
			{
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