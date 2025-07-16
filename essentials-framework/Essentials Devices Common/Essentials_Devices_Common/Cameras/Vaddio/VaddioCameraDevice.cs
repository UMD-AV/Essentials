using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;

namespace VaddioCameraPlugin
{
    public sealed class VaddioCameraDevice : EssentialsBridgeableDevice, ICommunicationMonitor
    {
        public StatusMonitorBase CommunicationMonitor { get; private set; }
        private readonly IBasicCommunication _comms;
        private readonly CrestronQueue<VaddioCameraCommand> _commandQueue;
        private readonly CMutex _commandMutex;
        private readonly CTimer _commandTimer;
        private bool _queueWaiting;
        private bool _commandReady = true;
        private uint _lastCalledPreset;
        private readonly uint _homePreset;
        private EDirection _moveInProgress = EDirection.Stop;
        private eVaddioCameraCommand _lastInquiry = eVaddioCameraCommand.NoFeedback;
        private Dictionary<uint, uint> presetIds;

        private const long _pollTimeMs = 60000; // 60s
        private const long _warningTimeoutMs = 180000; // 180s
        private const long _errorTimeoutMs = 300000; // 300s

        private readonly uint? _privacyOnPreset;
        private readonly uint? _privacyOffPreset;

        private bool _power;

        /// <summary>
        /// Power feedback
        /// </summary>
        public BoolFeedback PowerFeedback { get; private set; }

        /// <summary>
        /// Power property
        /// </summary>
        public bool Power
        {
            get { return _power; }
            set
            {
                //Change error timeout to longer if power is off
                CommunicationMonitor.ErrorTime = value ? _errorTimeoutMs : 900000;
                if (_power == value) return;
                if (Power == false)
                {
                    ActivePreset = 0;
                }

                _power = value;
                PowerFeedback.FireUpdate();
            }
        }

        private bool _privacyOn;

        /// <summary>
        /// Privacy On feedback
        /// </summary>
        public BoolFeedback PrivacyOnFeedback { get; private set; }

        /// <summary>
        /// Power property
        /// </summary>
        public bool PrivacyOn
        {
            get { return _privacyOn; }
            set
            {
                if (_privacyOn == value) return;
                _privacyOn = value;
                PrivacyOnFeedback.FireUpdate();
            }
        }

        private bool _autoFocus;

        /// <summary>
        /// Autofocus feedback
        /// </summary>
        public BoolFeedback AutoFocusFeedback { get; private set; }

        /// <summary>
        /// Autofocus property
        /// </summary>
        public bool AutoFocus
        {
            get { return _autoFocus; }
            set
            {
                if (_autoFocus == value) return;
                _autoFocus = value;
                AutoFocusFeedback.FireUpdate();
            }
        }

        private const int PresetMax = 16;
        private int _presetCount;

        /// <summary>
        /// Preset count feedback
        /// </summary>
        public IntFeedback PresetCountFeedback { get; private set; }

        /// <summary>
        /// Preset count property
        /// </summary>
        public uint PresetCount
        {
            get { return (uint)_presetCount; }
            set
            {
                if (_presetCount == value) return;
                _presetCount = (int)value;
                PresetCountFeedback.FireUpdate();
            }
        }

        private int _activePreset;

        /// <summary>
        /// Preset count feedback
        /// </summary>
        public IntFeedback ActivePresetFeedback { get; private set; }

        /// <summary>
        /// Preset count property
        /// </summary>
        public uint ActivePreset
        {
            get { return (uint)_activePreset; }
            set
            {
                if (_activePreset == value) return;
                _activePreset = (int)value;
                ActivePresetFeedback.FireUpdate();
                foreach (KeyValuePair<uint, BoolFeedback> feedback in PresetActiveFeedbacks)
                {
                    feedback.Value.FireUpdate();
                }
            }
        }

        /// <summary>
        /// Preset name feedbacks
        /// </summary>
        public Dictionary<uint, StringFeedback> PresetNameFeedbacks { get; private set; }

        /// <summary>
        /// Preset active feedbacks
        /// </summary>
        public Dictionary<uint, BoolFeedback> PresetActiveFeedbacks { get; private set; }

        private const uint PanSpeedDefault = 9; // 00...18 (hex)
        private const uint PanSpeedMax = 18;
        private uint _panSpeed = PanSpeedDefault;

        /// <summary>
        /// Pan speed
        /// </summary>
        public uint PanSpeed
        {
            get { return _panSpeed; }
            set
            {
                if (_panSpeed == value) return;
                _panSpeed = (value < 1 || value > PanSpeedMax) ? PanSpeedDefault : value;
            }
        }

        private const uint TiltSpeedDefault = 9; // 00...18 (hex)
        private const uint TiltSpeedMax = 18;
        private uint _tiltSpeed = TiltSpeedDefault;

        /// <summary>
        /// Tilt speed
        /// </summary>
        public uint TiltSpeed
        {
            get { return _tiltSpeed; }
            set
            {
                if (_tiltSpeed == value) return;
                _tiltSpeed = (value < 1 || value > TiltSpeedMax) ? TiltSpeedDefault : value;
            }
        }

        private const uint ZoomSpeedDefault = 4; // 00...07 (hex)
        private const uint ZoomSpeedMax = 7;
        private uint _zoomSpeed = ZoomSpeedDefault;

        /// <summary>
        /// Zoom speed
        /// </summary>
        public uint ZoomSpeed
        {
            get { return _zoomSpeed; }
            set
            {
                if (_zoomSpeed == value) return;
                _zoomSpeed = (value < 1 || value > ZoomSpeedMax) ? ZoomSpeedDefault : value;
            }
        }

        private const uint FocusSpeedDefault = 4; // 00...07 (hex)
        private const uint FocusSpeedMax = 7;
        private uint _focusSpeed = FocusSpeedDefault;

        /// <summary>
        /// Focus speed
        /// </summary>
        public uint FocusSpeed
        {
            get { return _focusSpeed; }
            set
            {
                if (_focusSpeed == value) return;
                _focusSpeed = (value < 1 || value > FocusSpeedMax) ? FocusSpeedDefault : value;
            }
        }

        public class VaddioCameraCommand
        {
            public readonly eVaddioCameraCommand Command;
            public readonly string Text;

            public VaddioCameraCommand(eVaddioCameraCommand command, string text)
            {
                Command = command;
                Text = text;
            }
        }

        /// <summary>
        /// For tracking feedback responses from camera
        /// </summary>
        public enum eVaddioCameraCommand
        {
            PowerOnCmd,
            PowerOffCmd,
            PresetRecallCmd,
            PowerInquiry,
            FocusInquiry,
            PresetSave,
            PtzCommand,
            AutoFocusCommand,
            NoFeedback
        }

        /// <summary>
        /// Move PTZ direction enumeration
        /// </summary>
        public enum EDirection
        {
            Stop = 0,
            PanLeft = 1,
            PanRight = 2,
            TiltUp = 3,
            TiltDown = 4,
            ZoomIn = 5,
            ZoomOut = 6
        }

        /// <summary>
        /// Online feedback
        /// </summary>
        public BoolFeedback OnlineFeedback { get; private set; }

        /// <summary>
        /// Socket status feedback
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        /// Preset Saved Feedback
        /// </summary>
        public event EventHandler PresetSaved;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="config">device config</param>
        /// <param name="comms">IBasicCommunications</param>
        public VaddioCameraDevice(string key, string name, IBasicCommunication comms, VaddioCameraConfig config)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new Vaddio Camera instance");

            VaddioCameraConfig config1 = config;

            OnlineFeedback = new BoolFeedback(() => _comms != null && _comms.IsConnected);
            PowerFeedback = new BoolFeedback(() => Power);
            AutoFocusFeedback = new BoolFeedback(() => AutoFocus);
            PrivacyOnFeedback = new BoolFeedback(() => PrivacyOn);
            PresetCountFeedback = new IntFeedback(() => (int)PresetCount);
            PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();
            PresetActiveFeedbacks = new Dictionary<uint, BoolFeedback>();
            ActivePresetFeedback = new IntFeedback(() => (int)ActivePreset);

            if (config1.PanSpeed != null && config1.PanSpeed > 0 && config1.PanSpeed <= PanSpeedMax)
                PanSpeed = (uint)config1.PanSpeed;

            if (config1.TiltSpeed != null && config1.TiltSpeed > 0 && config1.TiltSpeed <= TiltSpeedMax)
                TiltSpeed = (uint)config1.TiltSpeed;

            if (config1.ZoomSpeed != null && config1.ZoomSpeed > 0 && config1.ZoomSpeed <= ZoomSpeedMax)
                ZoomSpeed = (uint)config1.ZoomSpeed;

            if (config1.FocusSpeed != null && config1.FocusSpeed > 0 && config1.FocusSpeed <= FocusSpeedMax)
                FocusSpeed = (uint)config1.FocusSpeed;

            if (config1.PrivacyOnPreset != null && config1.PrivacyOnPreset <= PresetMax)
                _privacyOnPreset = config1.PrivacyOnPreset;

            if (config1.PrivacyOffPreset != null && config1.PrivacyOffPreset <= PresetMax)
                _privacyOffPreset = config.PrivacyOffPreset;

            _homePreset = config.HomePreset ?? 1;

            _comms = comms;
            CommunicationGather commsGather = new CommunicationGather(_comms, "\r");
            commsGather.LineReceived += CommsGatherOnLineReceived;

            CommunicationMonitor = new GenericCommunicationMonitor(this, _comms, _pollTimeMs, _warningTimeoutMs,
                _errorTimeoutMs, Poll, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            _commandQueue = new CrestronQueue<VaddioCameraCommand>(10);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);

            ISocketStatus socket = _comms as ISocketStatus;
            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "Constructing new Vaddio Camera instance failed due to null socket");
            }

            InitializePresets(config1.Presets);
        }


        /// <summary>
        /// Use custom activate to connect the device and start the comms monitor
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            // Essentials will handle the connect method to the device
            _comms.Connect();
            // Essentials will handle starting the comms monitor
            CommunicationMonitor.Start();

            return base.CustomActivate();
        }


        private void InitializePresets(List<VaddioCameraPresetConfig> presets)
        {
            if (presets == null)
            {
                Debug.Console(0, this, "InitializePresets failed, preset dictionary is null");
                return;
            }

            Debug.Console(0, this, "Intializing presets");

            presetIds = new Dictionary<uint, uint>();
            PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();
            PresetActiveFeedbacks = new Dictionary<uint, BoolFeedback>();
            foreach (VaddioCameraPresetConfig preset in presets)
            {
                VaddioCameraPresetConfig p = preset;
                Debug.Console(0, this, "Preset {0} Name: {1}", p.Index, p.Name);
                uint viscaId = p.ViscaId ?? p.Index;

                presetIds.Add(p.Index, viscaId);
                PresetNameFeedbacks.Add(p.Index, new StringFeedback(() => p.Name));
                PresetActiveFeedbacks.Add(p.Index, new BoolFeedback(() => viscaId == ActivePreset));
            }
        }

        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Link to API method replaces bridge class, this method will be called by the bridge directly
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            VaddioCameraBridgeJoinMap joinMap = new VaddioCameraBridgeJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            Dictionary<string, JoinData> customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

            // link joins to bridge
            trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            // must null check so LinkToApi doesn't except when the device is TCP or UDP
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);

            // power on
            trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, SetPowerOn);
            PowerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
            // power off
            trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, SetPowerOff);
            PowerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

            // home
            trilist.SetBoolSigAction(joinMap.Home.JoinNumber, sig => RecallHomePosition());

            // pan
            trilist.SetBoolSigAction(joinMap.PanLeft.JoinNumber, sig => Move(sig, EDirection.PanLeft));
            trilist.SetBoolSigAction(joinMap.PanRight.JoinNumber, sig => Move(sig, EDirection.PanRight));

            // tilt
            trilist.SetBoolSigAction(joinMap.TiltUp.JoinNumber, sig => Move(sig, EDirection.TiltUp));
            trilist.SetBoolSigAction(joinMap.TiltDown.JoinNumber, sig => Move(sig, EDirection.TiltDown));

            // zoom
            trilist.SetBoolSigAction(joinMap.ZoomIn.JoinNumber, sig => Move(sig, EDirection.ZoomIn));
            trilist.SetBoolSigAction(joinMap.ZoomOut.JoinNumber, sig => Move(sig, EDirection.ZoomOut));

            // focus
            trilist.SetSigTrueAction(joinMap.AutoFocusOn.JoinNumber, () => AutoFocusSet(true));
            AutoFocusFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoFocusOn.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.AutoFocusOff.JoinNumber, () => AutoFocusSet(false));
            AutoFocusFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoFocusOff.JoinNumber]);

            // privacy
            trilist.SetBoolSigAction(joinMap.PrivacyOn.JoinNumber, sig =>
            {
                if (_privacyOnPreset != null)
                {
                    RecallPresetByNumber((uint)_privacyOnPreset);
                }
            });
            trilist.SetBoolSigAction(joinMap.PrivacyOff.JoinNumber, sig =>
            {
                if (_privacyOffPreset != null)
                {
                    RecallPresetByNumber((uint)_privacyOffPreset);
                }
            });
            PrivacyOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PrivacyOn.JoinNumber]);
            PrivacyOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PrivacyOff.JoinNumber]);
            UpdateFeedbacks();

            // preset saved
            PresetSaved += (o, a) =>
            {
                trilist.BooleanInput[joinMap.PresetSaved.JoinNumber].BoolValue = true;
                CrestronInvoke.BeginInvoke((obj) =>
                {
                    CrestronEnvironment.Sleep(3000);
                    trilist.BooleanInput[joinMap.PresetSaved.JoinNumber].BoolValue = false;
                });
            };

            // preset - analog recall and save by number
            trilist.SetUShortSigAction(joinMap.PresetRecallByNumber.JoinNumber, value =>
            {
                RecallPresetByNumber(value);
                Debug.Console(1, this, "LinkToApi PresetRecallByNumber[{0}] => RecallPreset({1})",
                    joinMap.PresetRecallByNumber.JoinNumber, value);
            });
            trilist.SetUShortSigAction(joinMap.PresetSaveByNumber.JoinNumber, value =>
            {
                SavePresetByNumber(value);
                Debug.Console(1, this, "LinkToApi PresetSaveByNumber[{0}] => SavePreset({1})",
                    joinMap.PresetSaveByNumber.JoinNumber, value);
            });
            ActivePresetFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresetRecallByNumber.JoinNumber]);

            // preset count feedback
            PresetCountFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresetCount.JoinNumber]);

            foreach (KeyValuePair<uint, StringFeedback> item in PresetNameFeedbacks)
            {
                // preset number
                ushort preset = (ushort)item.Key;

                // preset names
                uint nameJoin = preset + joinMap.PresetNames.JoinNumber - 1;
                StringFeedback nameFeedback = item.Value;
                nameFeedback.LinkInputSig(trilist.StringInput[nameJoin]);

                // preset recall
                uint recallJoin = preset + joinMap.PresetRecall.JoinNumber - 1;
                trilist.SetSigTrueAction(recallJoin, () =>
                {
                    if (presetIds.ContainsKey(preset))
                    {
                        RecallPresetByNumber(presetIds[preset]);
                        Debug.Console(1, this, "LinkToApi PresetRecall[{0}]: RecallPreset({1})", recallJoin, preset);
                    }
                });

                // preset save/store
                uint saveJoin = preset + joinMap.PresetSave.JoinNumber - 1;
                trilist.SetSigTrueAction(saveJoin, () =>
                {
                    if (presetIds.ContainsKey(presetIds[preset]))
                    {
                        SavePresetByNumber(preset);
                        Debug.Console(1, this, "LinkToApi PresetSave[{0}]: SavePreset({1})", saveJoin, preset);
                    }
                });
            }

            //Link boolean preset feedback
            foreach (KeyValuePair<uint, BoolFeedback> item in PresetActiveFeedbacks)
            {
                item.Value.LinkInputSig(trilist.BooleanInput[item.Key + joinMap.PresetRecall.JoinNumber - 1]);
            }

            // online status 
            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;
                trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
                UpdateFeedbacks();
            };
        }

        private void UpdateFeedbacks()
        {
            OnlineFeedback.FireUpdate();
            if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();

            PowerFeedback.FireUpdate();
            PresetCountFeedback.FireUpdate();
            PrivacyOnFeedback.FireUpdate();
            ActivePresetFeedback.FireUpdate();

            foreach (KeyValuePair<uint, StringFeedback> item in PresetNameFeedbacks)
                item.Value.FireUpdate();

            foreach (KeyValuePair<uint, BoolFeedback> item in PresetActiveFeedbacks)
                item.Value.FireUpdate();
        }

        #endregion

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            Debug.Console(0, this, args.Client.ClientStatus.ToString());

            UpdateFeedbacks();
        }

        private void commandTimeout(object o)
        {
            if (_lastInquiry == eVaddioCameraCommand.PowerInquiry)
            {
                //TODO: send ctl 5 to clear buffer?
                //CTRL 5

                Debug.Console(0, this,
                    "Power inquiry never received response, possible camera issue.");
            }

            _commandReady = true;
            ProcessQueue();
        }

        private void readyForNextCommand()
        {
            _commandTimer.Stop(); //No need for timeout on last command
            _commandReady = true;
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            CrestronInvoke.BeginInvoke((o) =>
            {
                //Thread safe queue processing below
                if (_queueWaiting == false)
                {
                    _queueWaiting = true;
                    if (_commandMutex.WaitForMutex())
                    {
                        Debug.Console(2, this, "Got command mutex");
                        _queueWaiting = false;
                        try
                        {
                            while (!_commandQueue.IsEmpty)
                            {
                                int count = 0;
                                while (!_commandReady && (count < 50))
                                {
                                    Thread.Sleep(100);
                                    count++;
                                }

                                VaddioCameraCommand cmd = _commandQueue.TryToDequeue();
                                _lastInquiry = cmd.Command;
                                _commandReady = false;
                                switch (_lastInquiry)
                                {
                                    case eVaddioCameraCommand.PtzCommand:
                                    case eVaddioCameraCommand.AutoFocusCommand:
                                        _commandTimer.Reset(100); //Wait maximum 100 ms for response
                                        break;

                                    default:
                                        _commandTimer
                                            .Reset(
                                                2000); //Wait maximum 2000 ms for response before sending the next command
                                        break;
                                }

                                CrestronInvoke.BeginInvoke((obj) => { SendText(cmd.Text); });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "Exception processing command queue: {0}", ex);
                        }
                        finally
                        {
                            _commandMutex.ReleaseMutex();
                            Debug.Console(2, this, "Released command mutex");
                        }
                    }
                    else
                    {
                        _queueWaiting = false;
                    }
                }
            });
        }

        public void QueueCommand(string text)
        {
            QueueCommand(eVaddioCameraCommand.NoFeedback, text);
        }

        public void QueueCommand(eVaddioCameraCommand inquiry, string text)
        {
            if (!_commandQueue.IsFull)
            {
                Debug.Console(2, this, "Queueing command: {0}", text);
                _commandQueue.TryToEnqueue(new VaddioCameraCommand(inquiry, text));
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Command queue is full! Dropping command.");
                readyForNextCommand();
            }
        }

        // line received gather
        private void CommsGatherOnLineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            if (args == null)
            {
                Debug.Console(2, this, "CommsGatherOnLineReceived: args are null");
                return;
            }

            if (string.IsNullOrEmpty(args.Text))
            {
                Debug.Console(2, this, "CommsGatherOnLineReceived: gahtered text is null or empty");
                return;
            }

            try
            {
                ProcessLineReceived(args.Text);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "CommsGatherOnLineReceived Exception Message: {0}", ex.Message);
            }
        }

        // process line received
        private void ProcessLineReceived(string response)
        {
            response = response.Trim();

            if (string.IsNullOrEmpty(response)) return;

            Debug.Console(1, this, "ProcessLineRecieved: '{0}', last inquiry: {1}", response, _lastInquiry.ToString());

            if (response.StartsWith(">"))
            {
                Debug.Console(1, this, "Received echo or prompt, ignoring. Last inquiry: {0}", _lastInquiry.ToString());
                return;
            }

            switch (response)
            {
                case "ERROR":
                    Debug.Console(0, this, "Received error response, last inquiry: {0}", _lastInquiry.ToString());
                    _lastInquiry = eVaddioCameraCommand.NoFeedback;
                    readyForNextCommand();
                    break;
                case "standby on":
                    Debug.Console(0, this, "Received error response, last inquiry: {0}", _lastInquiry.ToString());
                    _lastInquiry = eVaddioCameraCommand.NoFeedback;
                    readyForNextCommand();
                    break;
                case "OK":
                    Debug.Console(1, this, "Received execution confirmation, last inquiry: {0}",
                        _lastInquiry.ToString());
                    switch (_lastInquiry)
                    {
                        case eVaddioCameraCommand.PresetRecallCmd:
                            ActivePreset = _lastCalledPreset;
                            break;
                        case eVaddioCameraCommand.PresetSave:
                            ActivePreset = _lastCalledPreset;
                            PresetSavedFb();
                            break;
                        case eVaddioCameraCommand.PowerOnCmd:
                            Power = true;
                            CrestronInvoke.BeginInvoke((o) =>
                            {
                                CrestronEnvironment.Sleep(2000);
                                PollPower();
                            });
                            break;
                        case eVaddioCameraCommand.PowerOffCmd:
                            Power = false;
                            CrestronInvoke.BeginInvoke((o) =>
                            {
                                CrestronEnvironment.Sleep(2000);
                                PollPower();
                            });
                            break;
                    }

                    _lastInquiry = eVaddioCameraCommand.NoFeedback;
                    readyForNextCommand();
                    break;
                default:
                {
                    Regex regex = new Regex(@"^(?<cmd>.*):\S*(?<val>.*)", RegexOptions.CultureInvariant);
                    Match match = regex.Match(response);

                    if (!match.Success)
                    {
                        Debug.Console(0, this, "Received unknown feedback: {0}", response);
                        return;
                    }

                    string cmd = match.Groups["cmd"].Value.Trim();
                    string val = match.Groups["val"].Value.Trim();

                    switch (cmd)
                    {
                        case "standby":
                            Power = val != "on";
                            break;
                        case "auto_focus":
                            AutoFocus = val == "on";
                            break;
                        default:
                            Debug.Console(0, this, "Received unknown response: {0}", response);
                            break;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Send ASCII-formatted commands via IBasicCommunication object
        /// </summary>
        /// <param name="cmd"></param>
        public void SendText(string cmd)
        {
            if (!_comms.IsConnected)
                _comms.Connect();

            if (string.IsNullOrEmpty(cmd)) return;

            _comms.SendText(string.Format("{0}{1}", cmd, "\r"));
        }

        /// <summary>
        /// Poll 
        /// </summary>
        public void Poll()
        {
            try
            {
                // power inquiry
                PollPower();
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Exception in poll command: {0}", e.Message);
            }
        }

        public void PollPower()
        {
            QueueCommand(eVaddioCameraCommand.PowerInquiry, "camera standby get");
        }

        public void PollFocus()
        {
            QueueCommand(eVaddioCameraCommand.FocusInquiry, "camera focus mode get");
        }

        /// <summary>
        /// Set power state on
        /// </summary>
        public void SetPowerOn()
        {
            QueueCommand(eVaddioCameraCommand.PowerOnCmd, "camera standby off");
        }

        /// <summary>
        /// Set power state off
        /// </summary>
        public void SetPowerOff()
        {
            QueueCommand(eVaddioCameraCommand.PowerOffCmd, "camera standby on");
            ActivePreset = 0;
        }

        /// <summary>
        /// Move camera with automatic speed setting
        /// </summary>
        /// <param name="state">sig action true/false</param>
        /// <param name="direction">EMoveDirection direction</param>
        public void Move(bool state, EDirection direction)
        {
            if (state && _moveInProgress == EDirection.Stop)
            {
                int count = 0;
                uint slow = 0;
                uint medium = 0;
                uint fast = 0;

                _moveInProgress = direction;

                switch (direction)
                {
                    case EDirection.PanLeft:
                    case EDirection.PanRight:
                        slow = PanSpeed > 4 ? PanSpeed - 4 : 1;
                        medium = PanSpeed;
                        fast = PanSpeed < 14 ? PanSpeed + 4 : 18;
                        break;
                    case EDirection.TiltUp:
                    case EDirection.TiltDown:
                        slow = TiltSpeed > 4 ? TiltSpeed - 4 : 1;
                        medium = TiltSpeed;
                        fast = TiltSpeed < 14 ? TiltSpeed + 4 : 18;
                        break;
                    case EDirection.ZoomIn:
                    case EDirection.ZoomOut:
                        slow = ZoomSpeed > 2 ? ZoomSpeed - 2 : 1;
                        medium = ZoomSpeed;
                        fast = ZoomSpeed < 5 ? ZoomSpeed + 2 : 7;
                        break;
                }

                Move(direction, slow);

                CrestronInvoke.BeginInvoke((o) =>
                {
                    while (_moveInProgress == direction)
                    {
                        switch (count)
                        {
                            case 100:
                                Move(direction, medium);
                                break;
                            case 300:
                                Move(direction, fast);
                                break;
                        }

                        count++;
                        //Stop after 20s total
                        if (count > 2000)
                        {
                            Stop(direction);
                            break;
                        }

                        CrestronEnvironment.Sleep(10);
                    }
                });
            }
            else if (!state)
            {
                Stop(direction);
            }
        }

        /// <summary>
        /// Move camera with manual speed and manual stop
        /// </summary>
        /// <param name="direction">EMoveDirection direction</param>
        /// <param name="speed">speed to move</param>
        public void Move(EDirection direction, uint speed)
        {
            ActivePreset = 0;
            switch (direction)
            {
                case EDirection.PanLeft:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera pan left {0}", speed));
                    break;
                }
                case EDirection.PanRight:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera pan right {0}", speed));
                    break;
                }
                case EDirection.TiltUp:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera tilt up {0}", speed));
                    break;
                }
                case EDirection.TiltDown:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera tilt down {0}", speed));
                    break;
                }
                case EDirection.ZoomIn:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera zoom in {0}", speed));
                    break;
                }
                case EDirection.ZoomOut:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, string.Format("camera zoom out {0}", speed));
                    break;
                }
            }
        }

        /// <summary>
        /// Stop camera
        /// </summary>
        /// <param name="direction">EMoveDirection direction</param>
        public void Stop(EDirection direction)
        {
            _moveInProgress = EDirection.Stop;
            switch (direction)
            {
                case EDirection.PanLeft:
                case EDirection.PanRight:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, "camera pan stop");
                    break;
                }
                case EDirection.TiltUp:
                case EDirection.TiltDown:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, "camera tilt stop");
                    break;
                }
                case EDirection.ZoomIn:
                case EDirection.ZoomOut:
                {
                    QueueCommand(eVaddioCameraCommand.PtzCommand, "camera zoom stop");
                    break;
                }
            }
        }

        /// <summary>
        /// Autofocus on/off
        /// </summary>
        /// <param name="state">sig action true/false</param>
        public void AutoFocusSet(bool state)
        {
            string cmd = state
                ? "camera focus mode auto"
                : "camera focus mode manual";
            QueueCommand(eVaddioCameraCommand.AutoFocusCommand, cmd);
            PollFocus();
        }

        /// <summary>
        /// Recall Home Position
        /// </summary>
        public void RecallHomePosition()
        {
            _lastCalledPreset = 0;
            RecallPresetByNumber(_homePreset);
        }

        private void PresetSavedFb()
        {
            if (PresetSaved != null)
            {
                PresetSaved(this, null);
            }
        }

        /// <summary>
        /// Recall Preset by Number
        /// </summary>
        /// <param name="preset"></param>
        public void RecallPresetByNumber(uint preset)
        {
            if (preset <= 0)
                return;

            _lastCalledPreset = preset;
            QueueCommand(eVaddioCameraCommand.PresetRecallCmd, string.Format("camera preset recall {0}", preset));
        }

        /// <summary>
        /// Save Preset by Number
        /// </summary>
        /// <param name="preset">preset 1...16</param>
        public void SavePresetByNumber(uint preset)
        {
            if (preset <= 0)
                return;

            _lastCalledPreset = preset;
            QueueCommand(eVaddioCameraCommand.PresetSave, string.Format("camera preset store {0}", preset));
        }
    }
}