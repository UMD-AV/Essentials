using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Recording;
using PepperDash.Essentials.EpiphanPearl.Interfaces;
using PepperDash.Essentials.EpiphanPearl.JoinMaps;
using PepperDash.Essentials.EpiphanPearl.Models;
using PepperDash.Essentials.EpiphanPearl.Utilities;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlController : ReconfigurableBridgableDevice, ICommunicationMonitor, IDisposable
    {
        private const string RunningStatus = "running";
        private const string PausedStatus = "paused";

        private readonly IEpiphanPearlClient _client;
        private readonly EpiphanCommunicationMonitor _monitor;
        private BoolFeedback _nextEventExistsFeedback;
        private BoolFeedback _nextEventIn5mFeedback;
        private BoolFeedback _nextEventIn10mFeedback;
        private readonly string panoptoKey;
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
        private BoolFeedback _Extend5EnabledFeedback;
        private BoolFeedback _Extend15EnabledFeedback;
        private bool _Extend5Enabled;
        private bool _Extend15Enabled;

        private string _hdmiOutputSource;
        public StringFeedback HdmiOutputFeedback;

        private StringFeedback _runningEventStartFeedback;

        private List<Event> _scheduledEvents;
        private readonly CTimer _statusTimer;
        private readonly DeviceConfig devConfig;
        private IRecordingController _recordingController;

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

            panoptoKey = _devProperties.PanoptoKey ?? "";
            _monitor = new EpiphanCommunicationMonitor(this, 120000, 180000);
            _statusTimer = new CTimer(o => { GetRunningEventStatus(); }, null, Timeout.Infinite, 5000);
            CreateFeedbacks();
        }

        public override bool CustomActivate()
        {
            if (panoptoKey != "")
            {
                IKeyed device = DeviceManager.GetDeviceForKey(panoptoKey);
                _recordingController = device as IRecordingController;

                if (_recordingController != null)
                {
                    _recordingController.StartRecordingStatus.OutputChange += StartRecordingStatusChange;
                    _runningEventRunningFeedback.OutputChange += (o, args) =>
                    {
                        if (args.BoolValue)
                        {
                            //Reset recording start status on panopto controller once recording starts
                            _recordingController.ResetStartRecordingStatus();
                        }
                    };
                }
            }

            return true;
        }

        private void StartRecordingStatusChange(object sender, FeedbackEventArgs feedbackEventArgs)
        {
            try
            {
                if (feedbackEventArgs.StringValue.Contains("requested"))
                {
                    CrestronInvoke.BeginInvoke((o) =>
                    {
                        Debug.Console(1, this, "Getting scheduled events due to ad hoc start");
                        GetScheduledEvents();
                        int count = 0;
                        while (!_runningEventRunningFeedback.BoolValue && count < 120)
                        {
                            GetRunningEvent();

                            if (_scheduledEvents.Count > 0 && !_runningEventRunningFeedback.BoolValue)
                            {
                                if (_scheduledEvents[0].Start.ToLocalTime() < DateTime.Now.AddMinutes(5))
                                {
                                    Debug.Console(1, this, "Forcing ad hoc event start");
                                    StartEvent();
                                }
                            }

                            CrestronEnvironment.Sleep(1000);
                            count++;
                        }
                    });
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Exception after ad-hoc start: {0}", e);
            }
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        public override void Initialize()
        {
            _pollTimer = new CTimer(o => Poll(), null, 0, 30000);
            _monitor.Start();
        }

        private void Poll()
        {
            GetScheduledEvents();
            GetRunningEvent();
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
                    _runningEvent != null ? _runningEvent.Start.ToLocalTime().ToString("t") : string.Empty);
            _runningEventEndFeedback =
                new StringFeedback(() =>
                    _runningEvent != null ? _runningEvent.Finish.ToLocalTime().ToString("t") : string.Empty);
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
                          (_runningEvent.Status.Equals(RunningStatus, StringComparison.InvariantCultureIgnoreCase) ||
                           _runningEvent.Status.Equals(PausedStatus, StringComparison.InvariantCultureIgnoreCase)));

            _runningEventPausedFeedback =
                new BoolFeedback(
                    () => _runningEvent != null &&
                          _runningEvent.Status.Equals(PausedStatus, StringComparison.InvariantCultureIgnoreCase));

            _nextEventExistsFeedback = new BoolFeedback(() => _scheduledEvents.Count > 0);
            _nextEventIn5mFeedback = new BoolFeedback(() => _scheduledEvents.Count > 0 &&
                                                            _scheduledEvents[0].Start < DateTime.UtcNow.AddMinutes(5));

            _nextEventIn10mFeedback = new BoolFeedback(() => _scheduledEvents.Count > 0 &&
                                                             _scheduledEvents[0].Start <
                                                             DateTime.UtcNow.AddMinutes(10));

            _Extend5EnabledFeedback = new BoolFeedback(() => _Extend5Enabled);
            _Extend15EnabledFeedback = new BoolFeedback(() => _Extend15Enabled);

            HdmiOutputFeedback = new StringFeedback(() => _hdmiOutputSource);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            EpiphanPearlJoinMap joinMap = new EpiphanPearlJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
            trilist.StringInput[joinMap.PanoptoKey.JoinNumber].StringValue = panoptoKey;

            trilist.SetSigTrueAction(joinMap.Start.JoinNumber, StartEvent);
            trilist.SetSigTrueAction(joinMap.Stop.JoinNumber, StopRunningEvent);
            trilist.SetSigTrueAction(joinMap.Pause.JoinNumber, PauseRunningEvent);
            trilist.SetSigTrueAction(joinMap.Resume.JoinNumber, ResumeRunningEvent);
            trilist.SetSigTrueAction(joinMap.Extend5.JoinNumber, () => ExtendRunningEvent(5));
            trilist.SetSigTrueAction(joinMap.Extend15.JoinNumber, () => ExtendRunningEvent(15));

            trilist.SetStringSigAction(joinMap.HdmiOutputSource.JoinNumber, SetHdmiOutputSource);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);

            _runningEventRunningFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsRecording.JoinNumber]);
            _runningEventPausedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsPaused.JoinNumber]);
            _Extend5EnabledFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Extend5Enable.JoinNumber]);
            _Extend15EnabledFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Extend15Enable.JoinNumber]);


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
            _nextEventIn5mFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingIn5m.JoinNumber]);
            _nextEventIn10mFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingIn10m.JoinNumber]);

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
            _statusTimer.Reset(0, 5000);
        }

        private void StopEventStatusTimer()
        {
            _statusTimer.Stop();
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

            GetRunningEvent();
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

            GetRunningEvent();
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

            string path = string.Format("/schedule/events/{0}/control/start", id);

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
        }

        public void ExtendRunningEvent(ushort time)
        {
            string path = string.Format("/schedule/events/{0}/control/extend", _runningEvent.Id);

            ExtendEventRequest body = new ExtendEventRequest
            {
                Finish = _runningEvent.Finish + new TimeSpan(0, 0, time, 0)
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

            GetRunningEvent();
        }

        private void GetHdmiOutputSetting()
        {
            const string path = "/displays/D1/settings";

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
            string path = string.Format("/displays/D1/settings?source={0}", source);

            BaseResponse<string> response = _client.Post<BaseResponse<string>>(path);

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
            Debug.Console(1, this, "Getting Scheduled Events");
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
                    _scheduledRecordings[i].Start = _scheduledEvents[i].Start.ToLocalTime().ToString("t");
                    _scheduledRecordings[i].End = _scheduledEvents[i].Finish.ToLocalTime().ToString("t");

                    TimeSpan time = _scheduledEvents[i].Finish - _scheduledEvents[i].Start;
                    _scheduledRecordings[i].Length = string.Format("{0}", time);
                }
            }
            else
            {
                Debug.Console(2, this, "No Scheduled Events");
                foreach (ScheduledRecording t in _scheduledRecordings)
                {
                    t.Name = string.Empty;
                    t.Id = string.Empty;
                    t.Start = string.Empty;
                    t.End = string.Empty;
                    t.Length = string.Empty;
                }
            }

            UpdateFeedbacks();
        }

        private void GetRunningEvent()
        {
            // The current event could be either running or paused

            Debug.Console(1, this, "Getting Running Events");

            const string runningEventPath = "/schedule/events/?status=running";

            const string pausedEventPath = "/schedule/events/?status=paused";

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
                StartEventStatusTimer();
                return;
            }

            response = _client.Get<BaseResponse<List<Event>>>(pausedEventPath);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to get paused event");
                _runningEvent = null;

                UpdateFeedbacks();
                StopEventStatusTimer();
                return;
            }

            _runningEvent = response.Result.Count > 0 ? response.Result[0] : null;

            UpdateFeedbacks();

            if (_runningEvent != null)
            {
                StartEventStatusTimer();

                Debug.Console(1, this, "Running Event: {0} | {1} | {2} | {3} | ", _runningEvent.Id,
                    _runningEvent.Title, _runningEvent.Start, _runningEvent.Finish);
            }
            else
            {
                StopEventStatusTimer();
            }
        }

        private void GetRunningEventStatus()
        {
            Debug.Console(1, this, "Getting Running Event Status");
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No Running Event");
                return;
            }

            //Get event status
            string path = string.Format("/schedule/events/{0}", _runningEvent.Id);

            BaseResponse<Event> response = _client.Get<BaseResponse<Event>>(path);
            if (response == null)
            {
                Debug.Console(1, this, "Unable to get running event status");
                return;
            }

            try
            {
                _runningEvent = response.Result;
            }
            catch (Exception)
            {
                Debug.Console(1, this, "Unable to get running event finish time");
                return;
            }

            if (_scheduledEvents.Count > 0)
            {
                Debug.Console(1, this, "Scheduled event found, calculating extend enable: {0}, {1}",
                    _scheduledEvents[0].Start.ToLocalTime().ToString("t"),
                    _runningEvent.Finish.ToLocalTime().ToString("t"));
                _Extend5Enabled = _scheduledEvents[0].Start >=
                    _runningEvent.Finish.AddMinutes(6) && _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                _Extend15Enabled = _scheduledEvents[0].Start >=
                    _runningEvent.Finish.AddMinutes(16) && _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
            }
            else
            {
                Debug.Console(1, this, "No scheduled event found, extend is enabled");
                _Extend5Enabled = _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                _Extend15Enabled = _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
            }

            _runningEventRunningFeedback.FireUpdate();
            _runningEventPausedFeedback.FireUpdate();
            _Extend5EnabledFeedback.FireUpdate();
            _Extend15EnabledFeedback.FireUpdate();
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
            _Extend5EnabledFeedback.FireUpdate();
            _Extend15EnabledFeedback.FireUpdate();
            _nextEventExistsFeedback.FireUpdate();
            _nextEventIn5mFeedback.FireUpdate();
            _nextEventIn10mFeedback.FireUpdate();
            HdmiOutputFeedback.FireUpdate();

            foreach (ScheduledRecording t in _scheduledRecordings)
            {
                t.NameFeedback.FireUpdate();
                t.IdFeedback.FireUpdate();
                t.StartFeedback.FireUpdate();
                t.EndFeedback.FireUpdate();
                t.LengthFeedback.FireUpdate();
            }
        }

        protected override void CustomSetConfig(DeviceConfig config)
        {
            ConfigWriter.UpdateDeviceConfig(config);
        }

        public void Dispose()
        {
            if (_pollTimer != null) _pollTimer.Dispose();
            if (_statusTimer != null) _statusTimer.Dispose();
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

            NameFeedback = new StringFeedback(() => Name);
            IdFeedback = new StringFeedback(() => Id);
            StartFeedback = new StringFeedback(() => Start);
            EndFeedback = new StringFeedback(() => End);
            LengthFeedback = new StringFeedback(() => Length);
        }
    }
}