using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.EpiphanPearl.Interfaces;
using PepperDash.Essentials.EpiphanPearl.JoinMaps;
using PepperDash.Essentials.EpiphanPearl.Models;
using PepperDash.Essentials.EpiphanPearl.Utilities;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlController : ReconfigurableBridgableDevice, ICommunicationMonitor
    {
        private const string RunningStatus = "running";
        private const string PausedStatus = "paused";

        private readonly IEpiphanPearlClient _client;
        private readonly EpiphanCommunicationMonitor _monitor;
        private BoolFeedback _nextEventExistsFeedback;

        private CTimer _pollTimer;

        private Event _runningEvent;

        private List<ScheduledRecording> _scheduledRecordings;

        private StringFeedback _runningEventEndFeedback;
        private StringFeedback _runningEventIdFeedback;
        private StringFeedback _runningEventLengthFeedback;
        private StringFeedback _runningEventNameFeedback;
        private StringFeedback _runningEventTimeRemainingFeedback;

        private BoolFeedback _runningEventRunningFeedback;
        private BoolFeedback _runningEventPausedFeedback;

        private string _hdmiOutputSource;
        public StringFeedback HdmiOutputFeedback;

        private StringFeedback _runningEventStartFeedback;

        private List<Event> _scheduledEvents;
        private CTimer _statusTimer;
        private DeviceConfig devConfig;

        private EpiphanPearlControllerConfiguration _devProperties
        {
            get { return devConfig.Properties.ToObject<EpiphanPearlControllerConfiguration>(); }
        }

        public EpiphanPearlController(DeviceConfig config) : base(config)
        {
            devConfig = config;

            if (_devProperties.Secure)
            {
                _client = new EpiphanPearlSecureClient(_devProperties.Host, _devProperties.Username,
                    _devProperties.Password);
            }
            else
            {
                _client = new EpiphanPearlClient(_devProperties.Host, _devProperties.Username, _devProperties.Password);
            }

            _monitor = new EpiphanCommunicationMonitor(this, 30000, 60000);

            CreateFeedbacks();
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        public override void Initialize()
        {
            _pollTimer = new CTimer(o => Poll(), null, 0, 10000);

            _monitor.Start();
        }

        private void Poll()
        {
            Debug.Console(1, this, "Getting Scheduled Events");
            GetScheduledEvents();

            Debug.Console(1, this, "Getting Running Events");
            GetRunningEvent();

            Debug.Console(1, this, "Getting Running Event Status");
            GetRunningEventStatus();
        }

        private void CreateFeedbacks()
        {
            _scheduledRecordings = new List<ScheduledRecording>();

            for (int i = 0; i < 20; i++)
            {
                ScheduledRecording recording = new ScheduledRecording();
                _scheduledRecordings.Add(recording);
            }

            _runningEventNameFeedback =
                new StringFeedback(() => _runningEvent != null ? _runningEvent.Title : string.Empty);
            _runningEventStartFeedback =
                new StringFeedback(() =>
                    _runningEvent != null ? _runningEvent.Start.ToLocalTime().ToString("hh:mm:ss tt") : string.Empty);
            _runningEventEndFeedback =
                new StringFeedback(() =>
                    _runningEvent != null ? _runningEvent.Finish.ToLocalTime().ToString("hh:mm:ss tt") : string.Empty);
            _runningEventIdFeedback = new StringFeedback(() => _runningEvent != null ? _runningEvent.Id : string.Empty);
            _runningEventLengthFeedback = new StringFeedback(() =>
            {
                if (_runningEvent == null)
                {
                    return string.Empty;
                }

                TimeSpan length = _runningEvent.Finish - _runningEvent.Start;

                return string.Format("{0}", length);
            });
            _runningEventTimeRemainingFeedback = new StringFeedback(() =>
            {
                if (_runningEvent == null)
                {
                    return string.Empty;
                }

                DateTime currentTime = DateTime.UtcNow;
                TimeSpan timeRemaining = _runningEvent.Finish.Subtract(currentTime);

                return string.Format("{0}", ((timeRemaining.Hours * 60) + timeRemaining.Minutes));
            });

            _runningEventRunningFeedback =
                new BoolFeedback(
                    () => _runningEvent != null &&
                          _runningEvent.Status.Equals(RunningStatus, StringComparison.InvariantCultureIgnoreCase));

            _runningEventPausedFeedback =
                new BoolFeedback(
                    () => _runningEvent != null &&
                          _runningEvent.Status.Equals(PausedStatus, StringComparison.InvariantCultureIgnoreCase));

            _nextEventExistsFeedback = new BoolFeedback(() => _scheduledEvents.Count > 0);

            HdmiOutputFeedback = new StringFeedback(() => _hdmiOutputSource);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            EpiphanPearlJoinMap joinMap = new EpiphanPearlJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;

            trilist.SetSigTrueAction(joinMap.Start.JoinNumber, StartEvent);
            trilist.SetSigTrueAction(joinMap.Stop.JoinNumber, StopRunningEvent);
            trilist.SetSigTrueAction(joinMap.Pause.JoinNumber, PauseRunningEvent);
            trilist.SetSigTrueAction(joinMap.Resume.JoinNumber, ResumeRunningEvent);
            trilist.SetSigTrueAction(joinMap.Extend.JoinNumber, ExtendRunningEvent);
            
            trilist.SetStringSigAction(joinMap.HdmiOutputSource.JoinNumber, SetHdmiOutputSource);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);

            _runningEventRunningFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsRecording.JoinNumber]);
            _runningEventPausedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsPaused.JoinNumber]);

            _runningEventNameFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingName.JoinNumber]);
            _runningEventStartFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingStartTime.JoinNumber]);
            _runningEventEndFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingEndTime.JoinNumber]);
            _runningEventIdFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingId.JoinNumber]);
            _runningEventLengthFeedback.LinkInputSig(trilist.StringInput[joinMap.CurrentRecordingLength.JoinNumber]);
            _runningEventTimeRemainingFeedback.LinkInputSig(
                trilist.StringInput[joinMap.CurrentRecordingTimeRemaining.JoinNumber]);

            _scheduledRecordings[0].NameFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingName.JoinNumber]);
            _scheduledRecordings[0].IdFeedback.LinkInputSig(trilist.StringInput[joinMap.NextRecordingId.JoinNumber]);
            _scheduledRecordings[0].StartFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingStartTime.JoinNumber]);
            _scheduledRecordings[0].EndFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingEndTime.JoinNumber]);
            _scheduledRecordings[0].LengthFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingLength.JoinNumber]);

            _nextEventExistsFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingExists.JoinNumber]);

            HdmiOutputFeedback.LinkInputSig((trilist.StringInput[joinMap.HdmiOutputSource.JoinNumber]));

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;

                trilist.StringInput[joinMap.CurrentRecordingId.JoinNumber].StringValue =
                    _runningEventIdFeedback.StringValue;
                trilist.StringInput[joinMap.CurrentRecordingName.JoinNumber].StringValue =
                    _runningEventNameFeedback.StringValue;
                trilist.StringInput[joinMap.CurrentRecordingStartTime.JoinNumber].StringValue =
                    _runningEventStartFeedback.StringValue;
                trilist.StringInput[joinMap.CurrentRecordingEndTime.JoinNumber].StringValue =
                    _runningEventEndFeedback.StringValue;
                trilist.StringInput[joinMap.CurrentRecordingLength.JoinNumber].StringValue =
                    _runningEventLengthFeedback.StringValue;
                trilist.StringInput[joinMap.CurrentRecordingTimeRemaining.JoinNumber].StringValue =
                    _runningEventTimeRemainingFeedback.StringValue;

                Debug.Console(2, this, "Bridge online.");

                Debug.Console(2, this, "{0} - {1} | {2} | {3} | {4} | {5}", 0, _scheduledRecordings[0].Id,
                    _scheduledRecordings[0].Name, _scheduledRecordings[0].Start, _scheduledRecordings[0].End,
                    _scheduledRecordings[0].Length);

                trilist.StringInput[joinMap.NextRecordingId.JoinNumber].StringValue = _scheduledRecordings[0].Id;
                trilist.StringInput[joinMap.NextRecordingName.JoinNumber].StringValue = _scheduledRecordings[0].Name;
                trilist.StringInput[joinMap.NextRecordingStartTime.JoinNumber].StringValue =
                    _scheduledRecordings[0].Start;
                trilist.StringInput[joinMap.NextRecordingEndTime.JoinNumber].StringValue = _scheduledRecordings[0].End;
                trilist.StringInput[joinMap.NextRecordingLength.JoinNumber].StringValue =
                    _scheduledRecordings[0].Length;
            };
        }

        private void StartEventStatusTimer()
        {
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                _statusTimer = null;
            }

            _statusTimer = new CTimer(o => GetRunningEventStatus(), null, 0, 5000);
        }

        private void StopEventStatusTimer()
        {
            if (_statusTimer == null) return;

            _statusTimer.Stop();
            _statusTimer.Dispose();
            _statusTimer = null;
        }

        public void PauseRunningEvent()
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No running event");
                return;
            }

            string path = string.Format("/schedule/events/{0}/control/pause", _runningEvent.Id);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to pause event");

                _monitor.SetOnlineStatus(false);

                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error pausing event: {0}", response.Message);
            }
        }

        public void ResumeRunningEvent()
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No running event");
                return;
            }

            string path = string.Format("/schedule/events/{0}/control/resume", _runningEvent.Id);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to resume event");

                _monitor.SetOnlineStatus(false);

                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error resuming event: {0}", response.Message);
            }
        }

        public void StopRunningEvent()
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No running event");
                return;
            }

            string path = string.Format("/schedule/events/{0}/control/stop", _runningEvent.Id);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to stop event");

                _monitor.SetOnlineStatus(false);

                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error stopping event: {0}", response.Message);
            }

            //StopEventStatusTimer();

            GetRunningEventStatus();

            GetRunningEvent();
        }

        public void StartEvent()
        {
            string id = string.Empty;
            if (_scheduledEvents.Count > 0)
            {
                id = _scheduledEvents[0].Id;
            }

            if (string.IsNullOrEmpty(id))
            {
                Debug.Console(1, this, "No scheduled event to start");
                return;
            }

            string path = string.Format("/schedule/events/{0}/control/stop", id);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to start event");

                _monitor.SetOnlineStatus(false);

                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error starting event: {0}", response.Message);
                return;
            }

            GetRunningEvent();

            //StartEventStatusTimer();
        }

        public void ExtendRunningEvent()
        {
            string path = string.Format("/schedule/events/{0}/control/extend", _runningEvent.Id);

            ExtendEventRequest body = new ExtendEventRequest
            {
                Finish = _runningEvent.Finish + new TimeSpan(0, 0, 15, 0)
            };

            BaseResponse<string> response = _client.Post<ExtendEventRequest, BaseResponse<string>>(path, body);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to extend event");

                _monitor.SetOnlineStatus(false);

                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error extending event: {0}", response.Message);
            }
        }

        private void GetHdmiOutputSetting()
        {
            string path = string.Format("api/displays/D1/settings");

            BaseResponse<HdmiResult> response = _client.Put<BaseResponse<HdmiResult>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get HDMI output setting");
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(0, this, "Error getting HDMI output setting: {0}", response.Message);
                return;
            }

            _hdmiOutputSource = response.Result.Source;
            HdmiOutputFeedback.FireUpdate();
        }

        /// <summary>
        /// Set the HDMI 1 output to a source. Example sources include:
        /// "1" for channel 1, "2" for channel 2, etc.
        /// "multiview" for the multiview output
        /// </summary>
        /// <param name="source"></param>
        public void SetHdmiOutputSource(string source)
        {
            string path = string.Format("api/displays/D1/settings?source={0}", source);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>("api/displays/D1/settings");

            if (response == null)
            {
                Debug.Console(1, this, "Unable to change HDMI output");
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.ConsoleWithLog(0, this, "Error changing HDMI output event: {0}", response.Message);
            }

            GetHdmiOutputSetting();
        }

        private void GetScheduledEvents()
        {
            DateTime from = DateTime.Now.Date;

            DateTime to = from + new TimeSpan(1, 0, 0, 0);

            string todayScheduledPath = string.Format("/schedule/events/?from={0}&to={1}&status=scheduled", from, to);

            BaseResponse<List<Event>> response = _client.Get<BaseResponse<List<Event>>>(todayScheduledPath);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get scheduled events");

                _scheduledEvents = new List<Event>();

                UpdateFeedbacks();

                _monitor.SetOnlineStatus(false);

                return;
            }

            _monitor.SetOnlineStatus(true);

            _scheduledEvents = response.Result;


            if (_scheduledEvents.Count > 0)
            {
                Debug.Console(2, this, "Scheduled Events");
                for (int i = 0; i < _scheduledEvents.Count; i++)
                {
                    Debug.Console(2, this, "{0} - {1} | {2} | {3} | {4}", i, _scheduledEvents[i].Id,
                        _scheduledEvents[i].Title, _scheduledEvents[i].Start, _scheduledEvents[i].Finish);
                    _scheduledRecordings[i].Name = _scheduledEvents[i].Title;
                    _scheduledRecordings[i].Id = _scheduledEvents[i].Id;
                    _scheduledRecordings[i].Start = _scheduledEvents[i].Start.ToLocalTime().ToString("hh:mm:ss tt");
                    _scheduledRecordings[i].End = _scheduledEvents[i].Finish.ToLocalTime().ToString("hh:mm:ss tt");

                    TimeSpan time = _scheduledEvents[i].Finish - _scheduledEvents[i].Start;
                    _scheduledRecordings[i].Length = string.Format("{0}", time);
                }
            }
            else
            {
                Debug.Console(2, this, "No Scheduled Events");
                for (int i = 0; i < _scheduledRecordings.Count; i++)
                {
                    _scheduledRecordings[i].Name = string.Empty;
                    _scheduledRecordings[i].Id = string.Empty;
                    _scheduledRecordings[i].Start = string.Empty;
                    _scheduledRecordings[i].End = string.Empty;
                    _scheduledRecordings[i].Length = string.Empty;
                }
            }

            UpdateFeedbacks();
        }

        private void GetRunningEvent()
        {
            // The current event could be either running or paused

            Debug.Console(1, this, "Getting Running Events");

            string runningEventPath = string.Format("/schedule/events/?status=running");

            string pausedEventPath = string.Format("/schedule/events/?status=paused");

            BaseResponse<List<Event>> response = _client.Get<BaseResponse<List<Event>>>(runningEventPath);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get running event");
            }

            if (response != null && response.Result.Count > 0)
            {
                _runningEvent = response.Result[0];

                Debug.Console(1, this, "Running Event: {0} | {1} | {2} | {3} | ", _runningEvent.Id,
                    _runningEvent.Title, _runningEvent.Start, _runningEvent.Finish);

                UpdateFeedbacks();

                //StartEventStatusTimer();
                return;
            }

            response = _client.Get<BaseResponse<List<Event>>>(pausedEventPath);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get paused event");
                _runningEvent = null;

                UpdateFeedbacks();
                //StopEventStatusTimer();

                return;
            }

            _runningEvent = response.Result.Count > 0 ? response.Result[0] : null;

            UpdateFeedbacks();

            if (_runningEvent != null)
            {
                //StartEventStatusTimer();

                Debug.Console(1, this, "Running Event: {0} | {1} | {2} | {3} | ", _runningEvent.Id,
                    _runningEvent.Title, _runningEvent.Start, _runningEvent.Finish);
            }
            else
            {
                //StopEventStatusTimer();
            }
        }

        private void GetRunningEventStatus()
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No Running Event");
                return;
            }

            string path = string.Format("/schedule/events/{0}/status", _runningEvent.Id);

            BaseResponse<string> response = _client.Get<BaseResponse<string>>(path);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get running event status");
                return;
            }

            _runningEvent.Status = response.Result;

            _runningEventRunningFeedback.FireUpdate();
            _runningEventPausedFeedback.FireUpdate();
        }

        private void UpdateFeedbacks()
        {
            _runningEventNameFeedback.FireUpdate();
            _runningEventStartFeedback.FireUpdate();
            _runningEventEndFeedback.FireUpdate();
            _runningEventIdFeedback.FireUpdate();
            _runningEventLengthFeedback.FireUpdate();
            _runningEventTimeRemainingFeedback.FireUpdate();

            _runningEventRunningFeedback.FireUpdate();
            _runningEventPausedFeedback.FireUpdate();
            _nextEventExistsFeedback.FireUpdate();

            HdmiOutputFeedback.FireUpdate();

            for (int i = 0; i < _scheduledRecordings.Count; i++)
            {
                _scheduledRecordings[i].NameFeedback.FireUpdate();
                _scheduledRecordings[i].IdFeedback.FireUpdate();
                _scheduledRecordings[i].StartFeedback.FireUpdate();
                _scheduledRecordings[i].EndFeedback.FireUpdate();
                _scheduledRecordings[i].LengthFeedback.FireUpdate();
            }
        }

        protected override void CustomSetConfig(DeviceConfig config)
        {
            ConfigWriter.UpdateDeviceConfig(config);
        }
    }

    public class ScheduledRecording
    {
        public StringFeedback NameFeedback { get; private set; }
        public StringFeedback IdFeedback { get; private set; }
        public StringFeedback StartFeedback { get; private set; }
        public StringFeedback EndFeedback { get; private set; }
        public StringFeedback LengthFeedback { get; private set; }

        public string Name { get; set; }

        public string Id { get; set; }

        public string Start { get; set; }

        public string End { get; set; }

        public string Length { get; set; }

        public ScheduledRecording()
        {
            Name = string.Empty;
            Id = string.Empty;
            Start = string.Empty;
            End = string.Empty;

            this.NameFeedback = new StringFeedback(() => this.Name);
            this.IdFeedback = new StringFeedback(() => this.Id);
            this.StartFeedback = new StringFeedback(() => this.Start);
            this.EndFeedback = new StringFeedback(() => this.End);
            this.LengthFeedback = new StringFeedback(() => this.Length);
        }
    }
}