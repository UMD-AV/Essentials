using System;
using System.Collections.Generic;
using System.Globalization;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Recording;
using PepperDash.Essentials.EpiphanPearl.JoinMaps;
using PepperDash.Essentials.EpiphanPearl.Models;
using PepperDash.Essentials.EpiphanPearl.Utilities;

namespace PepperDash.Essentials.EpiphanPearl
{
    public class EpiphanPearlController : ReconfigurableBridgableDevice, ICommunicationMonitor, IDisposable
    {
        private const string RunningStatus = "running";
        private const string PausedStatus = "paused";

        private readonly EpiphanPearlSecureClient _client;
        private readonly EpiphanCommunicationMonitor _monitor;

        private readonly string panoptoKey;
        private readonly CTimer _pollTimer;
        private readonly CTimer _vuMeterPollTimer;
        private Event _runningEvent;
        private readonly List<ScheduledRecording> _scheduledRecordings = new List<ScheduledRecording>();

        //Running event feedbacks
        private StringFeedback _runningEventEndFeedback;
        private StringFeedback _runningEventIdFeedback;
        private StringFeedback _runningEventLengthFeedback;
        private StringFeedback _runningEventNameFeedback;
        private StringFeedback _runningEventTimeRemainingFeedback;
        private BoolFeedback _runningEventRunningFeedback;
        private BoolFeedback _runningEventPausedFeedback;
        private BoolFeedback _Extend5EnabledFeedback;
        private BoolFeedback _Extend15EnabledFeedback;

        //Next event feedbacks
        private BoolFeedback _nextEventExistsFeedback;
        private BoolFeedback _nextEventIn5mFeedback;
        private BoolFeedback _nextEventIn10mFeedback;
        private StringFeedback _nextEventNameFeedback;
        private StringFeedback _nextEventIdFeedback;
        private StringFeedback _nextEventLengthFeedback;
        private StringFeedback _nextEventStartTimeFeedback;
        private StringFeedback _nextEventEndTimeFeedback;

        private bool _Extend5Enabled;
        private bool _Extend15Enabled;
        private string _hdmiOutputSource;
        public StringFeedback HdmiOutputFeedback;

        private readonly string _contentChannel;
        private readonly string _camera1Channel;
        private readonly string _camera2Channel;
        private readonly string _contentUrl;
        private readonly string _camera1Url;
        private readonly string _camera2Url;

        private string _contentLayout;
        public StringFeedback ContentLayoutFeedback;
        private string _camera1Layout;
        public StringFeedback Camera1LayoutFeedback;
        private string _camera2Layout;
        public StringFeedback Camera2LayoutFeedback;

        private readonly VideoPreview _contentPreview;
        private readonly VideoPreview _camera1Preview;
        private readonly VideoPreview _camera2Preview;

        private bool _enableVUMeterFeedback;

        public bool EnableVUMeterFeedback
        {
            get { return _enableVUMeterFeedback; }
            set
            {
                _enableVUMeterFeedback = value;
                if (value)
                {
                    StartVUMeterPoll();
                    if (_contentPreview != null)
                        _contentPreview.EnablePreview();
                    if (_camera1Preview != null)
                        _camera1Preview.EnablePreview();
                    if (_camera2Preview != null)
                        _camera2Preview.EnablePreview();
                }
                else
                {
                    if (_contentPreview != null)
                        _contentPreview.DisablePreview();
                    if (_camera1Preview != null)
                        _camera1Preview.DisablePreview();
                    if (_camera2Preview != null)
                        _camera2Preview.DisablePreview();
                }
            }
        }


        private ushort _vuMeterLevel;
        public IntFeedback VUMeterFeedback;
        public StringFeedback ContentUrlFeedback;
        public StringFeedback Camera1UrlFeedback;
        public StringFeedback Camera2UrlFeedback;

        private StringFeedback _runningEventStartFeedback;
        private readonly CTimer _statusTimer;
        private readonly CTimer _quickCheckTimer;
        private readonly DeviceConfig devConfig;
        private IRecordingController _recordingController;

        private EpiphanPearlControllerConfiguration _devProperties
        {
            get { return devConfig.Properties.ToObject<EpiphanPearlControllerConfiguration>(); }
        }

        public EpiphanPearlController(DeviceConfig config) : base(config)
        {
            devConfig = config;
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;
            _client = new EpiphanPearlSecureClient(Key, _devProperties.Host, _devProperties.Username,
                _devProperties.Password);

            panoptoKey = _devProperties.PanoptoKey ?? "";
            _monitor = new EpiphanCommunicationMonitor(this, 130000, 190000);
            _pollTimer = new CTimer(Poll, Timeout.Infinite);
            _vuMeterPollTimer = new CTimer(VUMeterPoll, Timeout.Infinite);
            _statusTimer = new CTimer(o => { GetRunningEventStatus(); }, null, Timeout.Infinite, 5000);
            _quickCheckTimer = new CTimer(o => { QuickCheckRunningEvent(); }, null, Timeout.Infinite, 5000);

            _contentChannel = _devProperties.contentChannel ?? "";
            _camera1Channel = _devProperties.camera1Channel ?? "";
            _camera2Channel = _devProperties.camera2Channel ?? "";

            if (!string.IsNullOrEmpty(_contentChannel))
            {
                _contentPreview = new VideoPreview(_client, "contentPreview",
                    string.Format("/channels/{0}/preview", _contentChannel), 8091);
                _contentUrl = string.Format("http://{0}.av.umd.edu:8091/preview/contentPreview.jpg",
                    EthernetHelper.LanHelper.Hostname);
            }
            else
            {
                _contentUrl = "";
            }

            if (!string.IsNullOrEmpty(_camera1Channel))
            {
                _camera1Preview = new VideoPreview(_client, "camera1Preview",
                    string.Format("/channels/{0}/preview", _camera1Channel), 8092);
                _camera1Url = string.Format("http://{0}.av.umd.edu:8092/preview/camera1Preview.jpg",
                    EthernetHelper.LanHelper.Hostname);
            }
            else
            {
                _camera1Url = "";
            }

            if (!string.IsNullOrEmpty(_camera2Channel))
            {
                _camera2Preview = new VideoPreview(_client, "camera2Preview",
                    string.Format("/channels/{0}/preview", _camera2Channel), 8093);
                _camera2Url = string.Format("http://{0}.av.umd.edu:8093/preview/camera2Preview.jpg",
                    EthernetHelper.LanHelper.Hostname);
            }
            else
            {
                _camera2Url = "";
            }

            _monitor.StatusChange += (sender, args) =>
            {
                if (args.Status == MonitorStatus.InError)
                {
                    _scheduledRecordings.Clear();
                    ClearRunningEvent();
                    UpdateFeedbacks();
                }
            };
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
                        GetEvents();
                        int count = 0;
                        while (!_runningEventRunningFeedback.BoolValue && count < 120)
                        {
                            GetEvents();

                            if (_scheduledRecordings.Count > 0 && !_runningEventRunningFeedback.BoolValue)
                            {
                                if (_scheduledRecordings[0].Start.ToLocalTime() < DateTime.Now.AddMinutes(5))
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
            _pollTimer.Reset(60000);
            _monitor.Start();
            GetLayouts();
        }

        private void Poll(object o)
        {
            _pollTimer.Reset(60000);
            GetEvents();
        }

        private void CreateFeedbacks()
        {
            _runningEventNameFeedback =
                new StringFeedback(() => _runningEvent != null ? _runningEvent.Title : string.Empty);
            _runningEventStartFeedback =
                new StringFeedback(() =>
                    _runningEvent != null
                        ? _runningEvent.Start.ToLocalTime().ToString("t", new CultureInfo("en-US"))
                        : string.Empty);
            _runningEventEndFeedback =
                new StringFeedback(() =>
                    _runningEvent != null
                        ? _runningEvent.Finish.ToLocalTime().ToString("t", new CultureInfo("en-US"))
                        : string.Empty);
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

                return string.Format("{0}", timeRemaining.Hours * 60 + timeRemaining.Minutes);
            });

            _runningEventRunningFeedback =
                new BoolFeedback(() => _runningEvent != null &&
                                       (_runningEvent.Status.Equals(RunningStatus,
                                            StringComparison.InvariantCultureIgnoreCase) ||
                                        _runningEvent.Status.Equals(PausedStatus,
                                            StringComparison.InvariantCultureIgnoreCase)));

            _runningEventPausedFeedback =
                new BoolFeedback(() => _runningEvent != null &&
                                       _runningEvent.Status.Equals(PausedStatus,
                                           StringComparison.InvariantCultureIgnoreCase));

            _nextEventExistsFeedback = new BoolFeedback(() => _scheduledRecordings.Count > 0);
            _nextEventIn5mFeedback = new BoolFeedback(() => _scheduledRecordings.Count > 0 &&
                                                            _scheduledRecordings[0].Start <
                                                            DateTime.UtcNow.AddMinutes(5));

            _nextEventIn10mFeedback = new BoolFeedback(() => _scheduledRecordings.Count > 0 &&
                                                             _scheduledRecordings[0].Start <
                                                             DateTime.UtcNow.AddMinutes(10));
            _nextEventIdFeedback =
                new StringFeedback(() => _scheduledRecordings.Count > 0
                    ? _scheduledRecordings[0].Id
                    : string.Empty);

            _nextEventNameFeedback =
                new StringFeedback(() => _scheduledRecordings.Count > 0
                    ? _scheduledRecordings[0].Name
                    : string.Empty);

            _nextEventLengthFeedback =
                new StringFeedback(() => _scheduledRecordings.Count > 0
                    ? _scheduledRecordings[0].Length
                    : string.Empty);

            _nextEventStartTimeFeedback =
                new StringFeedback(() => _scheduledRecordings.Count > 0
                    ? _scheduledRecordings[0].StartText
                    : string.Empty);

            _nextEventEndTimeFeedback =
                new StringFeedback(() => _scheduledRecordings.Count > 0
                    ? _scheduledRecordings[0].EndText
                    : string.Empty);

            _Extend5EnabledFeedback = new BoolFeedback(() => _Extend5Enabled);
            _Extend15EnabledFeedback = new BoolFeedback(() => _Extend15Enabled);

            HdmiOutputFeedback = new StringFeedback(() => _hdmiOutputSource);
            VUMeterFeedback = new IntFeedback(() => _vuMeterLevel);
            ContentLayoutFeedback = new StringFeedback(() => _contentLayout);
            Camera1LayoutFeedback = new StringFeedback(() => _camera1Layout);
            Camera2LayoutFeedback = new StringFeedback(() => _camera2Layout);
            ContentUrlFeedback = new StringFeedback(() => _contentUrl);
            Camera1UrlFeedback = new StringFeedback(() => _camera1Url);
            Camera2UrlFeedback = new StringFeedback(() => _camera2Url);
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
            trilist.SetBoolSigAction(joinMap.VUMeterEnable.JoinNumber, a => EnableVUMeterFeedback = a);

            trilist.SetStringSigAction(joinMap.HdmiOutputSource.JoinNumber, SetHdmiOutputSource);
            trilist.SetStringSigAction(joinMap.ContentLayout.JoinNumber,
                (layout) => SetLayout(ushort.Parse(_contentLayout), layout));
            trilist.SetStringSigAction(joinMap.Camera1Layout.JoinNumber,
                (layout) => SetLayout(ushort.Parse(_camera1Layout), layout));
            trilist.SetStringSigAction(joinMap.Camera2Layout.JoinNumber,
                (layout) => SetLayout(ushort.Parse(_camera2Layout), layout));

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.RecorderOnline.JoinNumber]);

            //running event
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

            //next event
            _nextEventNameFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingName.JoinNumber]);
            _nextEventIdFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingId.JoinNumber]);
            _nextEventStartTimeFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingStartTime.JoinNumber]);
            _nextEventEndTimeFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingEndTime.JoinNumber]);
            _nextEventLengthFeedback
                .LinkInputSig(trilist.StringInput[joinMap.NextRecordingLength.JoinNumber]);
            _nextEventExistsFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingExists.JoinNumber]);
            _nextEventIn5mFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingIn5m.JoinNumber]);
            _nextEventIn10mFeedback.LinkInputSig(trilist.BooleanInput[joinMap.NextRecordingIn10m.JoinNumber]);

            HdmiOutputFeedback.LinkInputSig(trilist.StringInput[joinMap.HdmiOutputSource.JoinNumber]);
            ContentLayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.ContentLayout.JoinNumber]);
            Camera1LayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.Camera1Layout.JoinNumber]);
            Camera2LayoutFeedback.LinkInputSig(trilist.StringInput[joinMap.Camera2Layout.JoinNumber]);
            ContentUrlFeedback.LinkInputSig(trilist.StringInput[joinMap.ContentUrl.JoinNumber]);
            Camera1UrlFeedback.LinkInputSig(trilist.StringInput[joinMap.Camera1Url.JoinNumber]);
            Camera2UrlFeedback.LinkInputSig(trilist.StringInput[joinMap.Camera2Url.JoinNumber]);
            VUMeterFeedback.LinkInputSig(trilist.UShortInput[joinMap.VUMeterFeedback.JoinNumber]);

            trilist.OnlineStatusChange += (device, args) =>
            {
                if (!args.DeviceOnLine) return;
                Debug.Console(2, this, "Bridge online.");
                UpdateFeedbacks();
            };
        }

        private void StartQuickCheckTimer()
        {
            _quickCheckTimer.Reset(0, 1000);
        }

        private void StartEventStatusTimer()
        {
            _quickCheckTimer.Stop();
            _statusTimer.Reset(0, 5000);
        }

        private void ClearRunningEvent()
        {
            _runningEvent = null;
            _statusTimer.Stop();
            _quickCheckTimer.Stop();
            UpdateRunningEventFeedbacks();
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
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error pausing event: {0}", response.Message);
            }
            else
            {
                _runningEvent.Status = PausedStatus;
                _runningEventPausedFeedback.FireUpdate();
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
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error resuming event: {0}", response.Message);
            }
            else
            {
                _runningEvent.Status = RunningStatus;
                _runningEventPausedFeedback.FireUpdate();
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
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error stopping event: {0}", response.Message);
            }
            else
            {
                ClearRunningEvent();
            }
        }

        public void StartEvent()
        {
            string id = string.Empty;
            if (_scheduledRecordings.Count > 0)
            {
                id = _scheduledRecordings[0].Id;
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
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error starting event: {0}", response.Message);
            }
            else
            {
                StartQuickCheckTimer();
                _scheduledRecordings.RemoveAll(r => r.Id == id);
                UpdateScheduledEventsFeedbacks();
            }
        }

        public void ExtendRunningEvent(ushort time)
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "No running event");
                return;
            }

            string path = string.Format("/schedule/events/{0}/control/extend", _runningEvent.Id);

            ExtendEventRequest body = new ExtendEventRequest
            {
                Finish = _runningEvent.Finish + new TimeSpan(0, 0, time, 0)
            };

            BaseResponse<string> response = _client.Post<ExtendEventRequest, BaseResponse<string>>(path, body);

            if (response == null)
            {
                Debug.Console(1, this, "Unable to extend event");
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.Console(1, this, "Error extending event: {0}", response.Message);
            }

            _runningEvent.Finish += new TimeSpan(0, 0, time, 0);
            _Extend5EnabledFeedback.FireUpdate();
            _Extend15EnabledFeedback.FireUpdate();
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

        /// <summary>
        /// Change the layout on a channel
        /// </summary>
        public void SetLayout(int channel, string layout)
        {
            if (layout.Length < 1 || channel < 1)
                return;

            string path = string.Format("/channels/{0}/layouts/active", channel);
            LayoutRequest body = new LayoutRequest
            {
                id = layout
            };

            BaseResponse<string> response = _client.Put<LayoutRequest, BaseResponse<string>>(path, body);
            if (response == null)
            {
                Debug.Console(0, this, "Unable to change channel layout");
                return;
            }

            if (!response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                Debug.ConsoleWithLog(0, this, "Error changing channel layout event: {0}", response.Message);
            }
            else
            {
                GetLayouts();
            }
        }

        private void GetLayouts()
        {
            if (!string.IsNullOrEmpty(_contentChannel))
            {
                BaseResponse<string> contentLayout =
                    _client.Get<BaseResponse<string>>(string.Format("/channels/{0}/layouts/active", _contentChannel));
                if (contentLayout == null)
                {
                    Debug.Console(1, this, "Unable to get content layout");
                    return;
                }

                if (contentLayout.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                {
                    _contentLayout = contentLayout.Result;
                    ContentLayoutFeedback.FireUpdate();
                }
            }

            if (!string.IsNullOrEmpty(_camera1Channel))
            {
                BaseResponse<string> camera1Layout =
                    _client.Get<BaseResponse<string>>(string.Format("/channels/{0}/layouts/active", _camera1Channel));
                if (camera1Layout == null)
                {
                    Debug.Console(1, this, "Unable to get camera1 layout");
                    return;
                }

                if (camera1Layout.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                {
                    _camera1Layout = camera1Layout.Result;
                    Camera1LayoutFeedback.FireUpdate();
                }
            }

            if (!string.IsNullOrEmpty(_camera2Channel))
            {
                BaseResponse<string> camera2Layout =
                    _client.Get<BaseResponse<string>>(string.Format("/channels/{0}/layouts/active", _camera2Channel));
                if (camera2Layout == null)
                {
                    Debug.Console(1, this, "Unable to get camera2 layout");
                    return;
                }

                if (camera2Layout.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                {
                    _camera2Layout = camera2Layout.Result;
                    Camera2LayoutFeedback.FireUpdate();
                }
            }
        }

        private void GetEvents()
        {
            Debug.Console(1, this, "Getting Today's Events");
            DateTime from = DateTime.Now.Date;
            DateTime to = from + new TimeSpan(1, 0, 0, 0);
            string todayScheduledPath = string.Format("/schedule/events/?from={0}&to={1}", from, to);
            BaseResponse<List<Event>> response = _client.Get<BaseResponse<List<Event>>>(todayScheduledPath);

            if (response != null && response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                _monitor.SetOnlineStatus(true);
                int scheduleCounter = 0;
                foreach (Event responseEvent in response.Result)
                {
                    switch (responseEvent.Status)
                    {
                        case "scheduled":
                        {
                            if (!_scheduledRecordings.Exists(x => x.Id == responseEvent.Id))
                            {
                                Debug.Console(2, this, "New scheduled recording {0} | {1} | {2} | {3}",
                                    responseEvent.Id, responseEvent.Title, responseEvent.Start, responseEvent.Finish);
                                ScheduledRecording recording = new ScheduledRecording
                                {
                                    Name = responseEvent.Title,
                                    Id = responseEvent.Id,
                                    Start = responseEvent.Start,
                                    End = responseEvent.Finish
                                };

                                _scheduledRecordings.Add(recording);
                                _scheduledRecordings.Sort((a, b) => a.Start.CompareTo(b.Start));
                                scheduleCounter++;
                            }

                            break;
                        }
                        case RunningStatus:
                        case PausedStatus:
                            if (_runningEvent == null)
                            {
                                _runningEvent = responseEvent;
                                UpdateRunningEventFeedbacks();
                                StartEventStatusTimer();
                                GetEvents();
                            }

                            break;
                    }
                }

                if (_scheduledRecordings.Count != scheduleCounter)
                {
                    _scheduledRecordings.RemoveAll(r =>
                        !response.Result.Exists(e => e.Id == r.Id && e.Status == "scheduled"));
                }

                if (_scheduledRecordings.Count > 0)
                {
                    if (DateTime.UtcNow.AddMinutes(5) > _scheduledRecordings[0].Start)
                    {
                        StartQuickCheckTimer();
                    }
                }

                UpdateScheduledEventsFeedbacks();
            }
            else
            {
                Debug.Console(1, this, "Unable to get scheduled events");
                _monitor.SetOnlineStatus(false);
            }
        }

        private void QuickCheckRunningEvent()
        {
            if (_runningEvent == null)
            {
                Debug.Console(1, this, "Getting Running Event");

                //Get event status
                BaseResponse<List<Event>> response =
                    _client.Get<BaseResponse<List<Event>>>("/schedule/events/?status=running");
                if (response != null && response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (response.Result.Count > 0 && _runningEvent == null)
                    {
                        _runningEvent = response.Result[0];
                        UpdateRunningEventFeedbacks();
                        StartEventStatusTimer();
                        GetEvents();
                    }
                }
            }
            else
            {
                _quickCheckTimer.Stop();
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
            if (response != null && response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
            {
                if (response.Result.Status == RunningStatus || response.Result.Status == PausedStatus)
                {
                    _runningEvent = response.Result;
                }
                else
                {
                    ClearRunningEvent();
                    return;
                }

                if (_scheduledRecordings.Count > 0 && _runningEvent != null)
                {
                    Debug.Console(1, this, "Scheduled event found, calculating extend enable: {0}, {1}",
                        _scheduledRecordings[0].StartText,
                        _runningEvent.Finish.ToLocalTime().ToString("t", new CultureInfo("en-US")));
                    _Extend5Enabled = _scheduledRecordings[0].Start >=
                        _runningEvent.Finish.AddMinutes(6) && _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                    _Extend15Enabled = _scheduledRecordings[0].Start >=
                        _runningEvent.Finish.AddMinutes(16) && _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                }
                else if (_runningEvent != null)
                {
                    Debug.Console(1, this, "No scheduled event found, extend is enabled");
                    _Extend5Enabled = _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                    _Extend15Enabled = _runningEvent.Finish < DateTime.UtcNow.AddHours(6);
                }

                UpdateRunningEventFeedbacks();
            }
            else if (response != null &&
                     response.Status.Equals("notfound", StringComparison.InvariantCultureIgnoreCase))
            {
                ClearRunningEvent();
                Debug.Console(1, this, "Unable to find running event by id");
            }
            else
            {
                Debug.Console(1, this, "Unable to get running event status");
            }
        }

        private void StartVUMeterPoll()
        {
            _vuMeterPollTimer.Reset(0);
        }


        public static ushort ScaleToUInt16(double value)
        {
            // Clamp input to -90 to 0 to avoid unexpected behavior
            value = Math.Max(-90, Math.Min(0, value));

            double scaled = (value + 90) * (65535.0 / 90.0);
            return (ushort)Math.Round(scaled);
        }

        private void VUMeterPoll(object o)
        {
            try
            {
                Debug.Console(0, this, "vu meter poll");
                BaseResponse<List<VUMeterResponse>> response =
                    _client.Get<BaseResponse<List<VUMeterResponse>>>("/sources/status?ids=D2P0.analog-a");
                if (response != null && response.Status.Equals("ok", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (response.Result != null && response.Result.Count > 0)
                    {
                        _vuMeterLevel = ScaleToUInt16(response.Result[0].Status.Audio.Levels.Rms[0]);
                        VUMeterFeedback.FireUpdate();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Exception in vu meter poll: {0}", e.Message);
            }
            finally
            {
                if (_enableVUMeterFeedback)
                {
                    if (_runningEventRunningFeedback.BoolValue)
                    {
                        _vuMeterPollTimer.Reset(1000);
                    }
                    else
                    {
                        _vuMeterPollTimer.Reset(100);
                    }
                }
            }
        }

        private void UpdateRunningEventFeedbacks()
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
        }

        private void UpdateScheduledEventsFeedbacks()
        {
            _nextEventExistsFeedback.FireUpdate();
            _nextEventIn5mFeedback.FireUpdate();
            _nextEventIn10mFeedback.FireUpdate();
            _nextEventIdFeedback.FireUpdate();
            _nextEventNameFeedback.FireUpdate();
            _nextEventLengthFeedback.FireUpdate();
            _nextEventStartTimeFeedback.FireUpdate();
            _nextEventEndTimeFeedback.FireUpdate();

            foreach (ScheduledRecording t in _scheduledRecordings)
            {
                t.NameFeedback.FireUpdate();
                t.IdFeedback.FireUpdate();
                t.StartFeedback.FireUpdate();
                t.EndFeedback.FireUpdate();
                t.LengthFeedback.FireUpdate();
            }
        }

        private void UpdateFeedbacks()
        {
            UpdateRunningEventFeedbacks();
            HdmiOutputFeedback.FireUpdate();
            ContentLayoutFeedback.FireUpdate();
            Camera1LayoutFeedback.FireUpdate();
            Camera2LayoutFeedback.FireUpdate();
            ContentUrlFeedback.FireUpdate();
            Camera1UrlFeedback.FireUpdate();
            Camera2UrlFeedback.FireUpdate();
            UpdateScheduledEventsFeedbacks();
        }

        protected override void CustomSetConfig(DeviceConfig config)
        {
            ConfigWriter.UpdateDeviceConfig(config);
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
            Dispose();
        }

        public void Dispose()
        {
            Debug.Console(0, "Disposing Epiphan Recorder");
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer.Dispose();
            }

            if (_vuMeterPollTimer != null)
            {
                _vuMeterPollTimer.Stop();
                _vuMeterPollTimer.Dispose();
            }

            if (_contentPreview != null)
            {
                _contentPreview.Dispose();
            }

            if (_camera1Preview != null)
            {
                _camera1Preview.Dispose();
            }

            if (_camera2Preview != null)
            {
                _camera2Preview.Dispose();
            }

            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
            }

            if (_quickCheckTimer != null)
            {
                _quickCheckTimer.Stop();
                _quickCheckTimer.Dispose();
            }

            if (_client != null) _client.Dispose();
            Debug.Console(0, "Disposing Epiphan Recorder Complete");
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

        public DateTime Start { get; set; }

        public string StartText
        {
            get { return Start.ToLocalTime().ToString("t", new CultureInfo("en-US")); }
        }

        public DateTime End { get; set; }

        public string EndText
        {
            get { return End.ToLocalTime().ToString("t", new CultureInfo("en-US")); }
        }

        public string Length
        {
            get
            {
                if (End > Start)
                {
                    return string.Format("{0}", End - Start);
                }

                return string.Empty;
            }
        }

        public ScheduledRecording()
        {
            NameFeedback = new StringFeedback(() => Name ?? string.Empty);
            IdFeedback = new StringFeedback(() => Id ?? string.Empty);
            StartFeedback = new StringFeedback(() => StartText);
            EndFeedback = new StringFeedback(() => EndText);
            LengthFeedback = new StringFeedback(() => Length ?? string.Empty);
        }
    }
}