using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharp.Net.Http;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;
using ViscaCameraPlugin;

namespace PepperDash.Essentials.AverCamera
{
    public class AverCameraDevice : EssentialsBridgeableDevice, ICommunicationMonitor, IDisposable
    {
        private readonly string hostname;
        private readonly string username;
        private readonly string password;
        private readonly string streamUrl;
        private readonly string streamUrlRtsp;
        private HttpClient client;
        private readonly AverCommunicationMonitor _monitor;
        private CTimer _pollTimer;
        private uint _pollTracker;
        private readonly IBasicCommunication _comms;
        private readonly CrestronQueue<ViscaCameraCommand> _commandQueue;
        private readonly CMutex _commandMutex;
        private readonly CTimer _commandTimer;
        private readonly CMutex _feedbackMutex;
        private byte[] _incomingBuffer = { };
        private bool _queueWaiting;
        private bool _commandReady = true;
        private bool _offlineIFClearSent;
        private uint _counter;
        protected readonly bool _autoTrackingCapable;
        protected uint _lastCalledPreset;
        private readonly uint _homePreset;
        private EDirection _moveInProgress = EDirection.Stop;
        protected eViscaCameraCommand _lastInquiry = eViscaCameraCommand.NoFeedback;

        private readonly ViscaCameraConfig _config;
        private Dictionary<uint, uint> presetIds;

        protected readonly byte _address = 0x81;
        protected readonly byte _feedbackAddress = 0x90;
        private const uint AddressMax = 7;
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
        protected bool Power
        {
            get { return _power; }
            set
            {
                if (_power == value) return;
                if (Power == false)
                {
                    ActivePreset = 0;
                }

                _power = value;
                PowerFeedback.FireUpdate();
            }
        }

        private bool _autoTrackingOn;

        /// <summary>
        /// Auto tracking on feedback
        /// </summary>
        public BoolFeedback AutoTrackingOnFeedback { get; private set; }

        /// <summary>
        /// Auto tracking on property
        /// </summary>
        protected bool AutoTrackingOn
        {
            get { return _autoTrackingOn; }
            set
            {
                if (_autoTrackingOn == value) return;
                _autoTrackingOn = value;
                ActivePreset = 0;
                AutoTrackingOnFeedback.FireUpdate();
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
        protected uint ActivePreset
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

        private bool _tallyOn;

        /// <summary>
        /// Tally on feedback
        /// </summary>
        public BoolFeedback TallyOnFeedback { get; private set; }

        /// <summary>
        /// Tally on property
        /// </summary>
        protected bool TallyOn
        {
            get { return _tallyOn; }
            set
            {
                if (_tallyOn == value) return;
                _tallyOn = value;
                TallyOnFeedback.FireUpdate();
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

        public class ViscaCameraCommand
        {
            public readonly eViscaCameraCommand Command;
            public readonly byte[] Bytes;

            public ViscaCameraCommand(eViscaCameraCommand command, byte[] bytes)
            {
                Command = command;
                Bytes = bytes;
            }
        }

        /// <summary>
        /// For tracking feedback responses from camera
        /// </summary>
        private enum eAverCameraInquiry
        {
            AutoTrackOnCmd,
            AutoTrackOffCmd,
            AutoTrackInquiry
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
        /// Auto Tracking Capable Feedback
        /// </summary>
        public BoolFeedback AutoTrackingCapable { get; private set; }

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
        /// <param name="commConfig"></param>
        public AverCameraDevice(string key, string name, IBasicCommunication comms, ViscaCameraConfig config,
            EssentialsControlPropertiesConfig commConfig)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new Aver Camera instance");

            _config = config;
            hostname = commConfig.TcpSshProperties.Address;
            username = commConfig.TcpSshProperties.Username;
            password = commConfig.TcpSshProperties.Password;
            streamUrl = config.StreamUrl;
            streamUrlRtsp = config.StreamUrlRtsp;

            BuildClient();
            _monitor = new AverCommunicationMonitor(this, 60000, 120000);
            OnlineFeedback = new BoolFeedback(() => _monitor.IsOnline);
            AutoTrackingCapable = new BoolFeedback(() => _autoTrackingCapable);
            PowerFeedback = new BoolFeedback(() => Power);
            AutoFocusFeedback = new BoolFeedback(() => AutoFocus);
            AutoTrackingOnFeedback = new BoolFeedback(() => AutoTrackingOn);
            PrivacyOnFeedback = new BoolFeedback(() => PrivacyOn);
            PresetCountFeedback = new IntFeedback(() => (int)PresetCount);
            PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();
            PresetActiveFeedbacks = new Dictionary<uint, BoolFeedback>();
            ActivePresetFeedback = new IntFeedback(() => (int)ActivePreset);
            TallyOnFeedback = new BoolFeedback(() => TallyOn);

            if (_config.AutoTracking)
                _autoTrackingCapable = true;

            _homePreset = _config.HomePreset ?? 1;

            if (_config.Address > 0 && _config.Address <= AddressMax && _config.Address != _address)
            {
                _address = Convert.ToByte(0x80 + _config.Address);
                _feedbackAddress = Convert.ToByte((_config.Address + 8) * 16);
            }

            if (_config.PanSpeed > 0 && _config.PanSpeed <= PanSpeedMax)
                PanSpeed = _config.PanSpeed;

            if (_config.TiltSpeed > 0 && _config.TiltSpeed <= TiltSpeedMax)
                TiltSpeed = _config.TiltSpeed;

            if (_config.ZoomSpeed > 0 && _config.ZoomSpeed <= ZoomSpeedMax)
                ZoomSpeed = _config.ZoomSpeed;

            if (_config.FocusSpeed > 0 && _config.FocusSpeed <= FocusSpeedMax)
                FocusSpeed = _config.FocusSpeed;

            if (_config.PrivacyOnPreset != null && _config.PrivacyOnPreset <= PresetMax)
                _privacyOnPreset = _config.PrivacyOnPreset;

            if (_config.PrivacyOffPreset != null && _config.PrivacyOffPreset <= PresetMax)
                _privacyOffPreset = config.PrivacyOffPreset;

            _comms = comms;
            _comms.BytesReceived += Handle_BytesReceived;
            DeviceManager.AddDevice(_monitor);

            _commandQueue = new CrestronQueue<ViscaCameraCommand>(10);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();

            ISocketStatus socket = _comms as ISocketStatus;
            if (socket != null)
            {
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }

            _monitor.StatusChange += (sender, args) => { OnlineFeedback.FireUpdate(); };

            InitializePresets(_config.Presets);
        }


        /// <summary>
        /// Use custom activate to connect the device and start the comms monitor
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            // Essentials will handle the connect method to the device
            _comms.Connect();
            _pollTimer = new CTimer(o => Poll(), null, 0, 30000);
            _monitor.Start();
            OnlineFeedback.FireUpdate();

            return base.CustomActivate();
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        private void BuildClient()
        {
            client = new HttpClient()
            {
                UserAgent = "crestron",
                KeepAlive = false,
                Accept = "text/plain",
                AllowAutoRedirect = false
            };
        }

        private void InitializePresets(List<ViscaCameraPresetConfig> presets)
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
            foreach (ViscaCameraPresetConfig preset in presets)
            {
                ViscaCameraPresetConfig p = preset;
                Debug.Console(0, this, "Preset {0} Name: {1}", p.Index, p.Name);
                uint viscaId = p.ViscaId ?? p.Index;

                presetIds.Add(p.Index, viscaId);
                PresetNameFeedbacks.Add(p.Index, new StringFeedback(() => p.Name));
                PresetActiveFeedbacks.Add(p.Index, new BoolFeedback(() => viscaId == ActivePreset));
            }
        }

        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Link to API method replaces bridge class, the bridge will call this method directly
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            ViscaCameraBridgeJoinMap joinMap = new ViscaCameraBridgeJoinMap(joinStart);

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

            AutoTrackingCapable.LinkInputSig(trilist.BooleanInput[joinMap.AutoTrackingCapable.JoinNumber]);

            OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
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

            // auto tracking on
            trilist.SetSigTrueAction(joinMap.AutoTrackingOn.JoinNumber, SetAutoTrackingOn);
            AutoTrackingOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoTrackingOn.JoinNumber]);

            // auto tracking off
            trilist.SetSigTrueAction(joinMap.AutoTrackingOff.JoinNumber, SetAutoTrackingOff);
            AutoTrackingOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoTrackingOff.JoinNumber]);

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
                CTimer unused = new CTimer(x => trilist.BooleanInput[joinMap.PresetSaved.JoinNumber].BoolValue = false,
                    3000);
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

            //stream url
            trilist.StringInput[joinMap.StreamUrl.JoinNumber].StringValue = streamUrl;
            trilist.StringInput[joinMap.StreamUrlRtsp.JoinNumber].StringValue = streamUrlRtsp;

            // online status 
            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;
                trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
                UpdateFeedbacks();
            };

            //tally light on/off
            TallyOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.TallyOn.JoinNumber]);
            TallyOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.TallyOff.JoinNumber]);
            trilist.SetSigTrueAction(joinMap.TallyOn.JoinNumber, SetTallyRed);
            trilist.SetSigTrueAction(joinMap.TallyOff.JoinNumber, SetTallyOff);
        }

        private void UpdateFeedbacks()
        {
            OnlineFeedback.FireUpdate();
            if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();

            PowerFeedback.FireUpdate();
            PresetCountFeedback.FireUpdate();
            AutoTrackingCapable.FireUpdate();
            AutoTrackingOnFeedback.FireUpdate();
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

            OnlineFeedback.FireUpdate();

            if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();

            if (!args.Client.IsConnected)
            {
                _commandQueue.Clear();
            }
            else
            {
                InitializeCamera();
            }
        }

        private void commandTimeout(object o)
        {
            if (_lastInquiry == eViscaCameraCommand.PowerInquiry && !_offlineIFClearSent)
            {
                _offlineIFClearSent = true;
                Debug.Console(0, this,
                    "Power inquiry never received response, possible camera issue. Sending IF Clear.");
                IFClear();
            }

            _commandReady = true;
            ProcessQueue();
        }

        protected void readyForNextCommand()
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

                                ViscaCameraCommand cmd = _commandQueue.TryToDequeue();
                                _lastInquiry = cmd.Command;
                                _commandReady = false;
                                switch (_lastInquiry)
                                {
                                    case eViscaCameraCommand.PtzCommand:
                                    case eViscaCameraCommand.AutoFocusCommand:
                                        _commandTimer.Reset(100); //Wait maximum 100 ms for response
                                        break;

                                    default:
                                        _commandTimer
                                            .Reset(2000);
                                        //Wait maximum 2000 ms for response before sending the next command
                                        break;
                                }

                                CrestronInvoke.BeginInvoke((obj) => { SendBytes(cmd.Bytes); });
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

        protected void QueueCommand(byte[] bytes)
        {
            QueueCommand(eViscaCameraCommand.NoFeedback, bytes);
        }

        protected void QueueCommand(eViscaCameraCommand inquiry, byte[] bytes)
        {
            if (!_commandQueue.IsFull)
            {
                Debug.Console(2, this, "Queueing command: {0}", ComTextHelper.GetEscapedText(bytes));
                _commandQueue.TryToEnqueue(new ViscaCameraCommand(inquiry, bytes));
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Command queue is full! Dropping command.");
                readyForNextCommand();
            }
        }

        /// <summary>
        /// Send bytes to the device
        /// </summary>
        /// <param name="bytes"></param>
        private void SendBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return;
            }

            Debug.Console(1, this, "Tx: {0}", ComTextHelper.GetEscapedText(bytes));
            // VISCA-over-IP counter
            if (_counter != 0xFFFFFFFF)
                _counter++;
            else
                _counter = 0;

            byte[] header =
            {
                0x01, 0x00, 0x00, Convert.ToByte(bytes.Length), (byte)(_counter << 8), (byte)(_counter << 16),
                (byte)(_counter << 24), (byte)_counter
            };

            byte[] cmd = new byte[header.Length + bytes.Length];
            header.CopyTo(cmd, 0);
            bytes.CopyTo(cmd, header.Length);
            _comms.SendBytes(cmd);
        }

        private void Handle_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            try
            {
                _feedbackMutex.WaitForMutex();
                if (_offlineIFClearSent)
                {
                    _offlineIFClearSent = false;
                }

                // Append the incoming bytes to whatever is in the buffer
                byte[] newBytes = new byte[_incomingBuffer.Length + e.Bytes.Length];
                _incomingBuffer.CopyTo(newBytes, 0);
                e.Bytes.CopyTo(newBytes, _incomingBuffer.Length);

                // Look for FF and process when found
                int start = 0;
                for (int i = 0; i < newBytes.Length; i++)
                {
                    if (newBytes[i] == 0xFF)
                    {
                        byte[] message = new byte[i - start + 1];
                        Array.Copy(newBytes, start, message, 0, i - start + 1);
                        start = i + 1;
                        CrestronInvoke.BeginInvoke((o) => ParseMessage(message));
                    }
                }

                int extraDataLength = newBytes.Length - start;
                if (extraDataLength > 0 && extraDataLength < 16)
                {
                    // Copy data after last FF to new incoming buffer
                    _incomingBuffer = new byte[extraDataLength];
                    Array.Copy(newBytes, start, _incomingBuffer, 0, extraDataLength);
                }
                else
                {
                    _incomingBuffer = new byte[] { };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning,
                    string.Format("Visca exception parsing feedback: {0}, {1}", ex.Message,
                        ComTextHelper.GetEscapedText(_incomingBuffer)));
            }
            finally
            {
                _feedbackMutex.ReleaseMutex();
            }
        }

        private void HttpParseMessage(eAverCameraInquiry request, string message)
        {
            Debug.Console(1, "Aver Camera Parsing: {0}, request: {1}", message, request.ToString());
            switch (request)
            {
                case eAverCameraInquiry.AutoTrackInquiry:
                    switch (message)
                    {
                        case "trk_tracking_on,3=0":
                        case "trk_tracking_on=0":
                            AutoTrackingOn = false;
                            _monitor.SetOnlineStatus(true);
                            Debug.Console(1, "Aver Camera AutoTrack Off");
                            break;
                        case "trk_tracking_on,3=1":
                        case "trk_tracking_on=1":
                            AutoTrackingOn = true;
                            _monitor.SetOnlineStatus(true);
                            Debug.Console(1, "Aver Camera AutoTrack On");
                            break;
                    }

                    break;
                case eAverCameraInquiry.AutoTrackOnCmd:
                    if (message.StartsWith("method return"))
                    {
                        AutoTrackingOn = true;
                    }

                    break;
                case eAverCameraInquiry.AutoTrackOffCmd:
                    if (message.StartsWith("method return"))
                    {
                        AutoTrackingOn = false;
                    }

                    break;
            }
        }

        private void ParseMessage(byte[] message)
        {
            Debug.Console(1, this, "Parsing: {0}, last inquiry: {1}", ComTextHelper.GetEscapedText(message),
                _lastInquiry.ToString());

            // Message: [0x90, 0x41, 0xFF]
            // 0xz0 = Address, z = device address + 8, or 9 for visca over IP
            // 0x4y = ACK (acknowledgment), y = socket number
            // 0xFF = Terminator
            if (message.Length > 2 && (message[message.Length - 2] >> 4) == 4 &&
                message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received ack");
                if (_lastInquiry == eViscaCameraCommand.PresetRecallCmd)
                {
                    ActivePreset = _lastCalledPreset;
                }

                return;
            }

            // Message: [0x90, 0x61, 0x41, 0xFF]
            // 0x6y = Error message, y = socket number
            // 0x41 = Command not executable
            // 0xFF = Terminator
            if (message.Length > 3 && (message[message.Length - 2] >> 4) == 4 &&
                (message[message.Length - 3] >> 4) == 6 && message[message.Length - 4] == _feedbackAddress)
            {
                switch (_lastInquiry)
                {
                    case eViscaCameraCommand.PowerInquiry:
                        Debug.Console(0, this,
                            "Power inquiry received command not executable, possible camera issue. Sending IF Clear.");
                        IFClear();
                        break;
                }

                Debug.Console(0, this, "Received command not executable");
                _lastInquiry = eViscaCameraCommand.NoFeedback;
                readyForNextCommand();
                return;
            }

            // Message: [0x90, 0x51, 0xFF]
            // 0x51 = Execution confirmation, 0xFF = Terminator
            if (message.Length > 2 && message[message.Length - 2] == 0x51 &&
                message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received execution confirmation, last inquiry: {0}", _lastInquiry.ToString());
                switch (_lastInquiry)
                {
                    case eViscaCameraCommand.PresetSave:
                        ActivePreset = _lastCalledPreset;
                        PresetSavedFb();
                        break;
                    case eViscaCameraCommand.PowerOnCmd:
                        Power = true;
                        CrestronInvoke.BeginInvoke((o) =>
                        {
                            CrestronEnvironment.Sleep(2000);
                            PollPower();
                        });
                        break;
                    case eViscaCameraCommand.PowerOffCmd:
                        Power = false;
                        CrestronInvoke.BeginInvoke((o) =>
                        {
                            CrestronEnvironment.Sleep(2000);
                            PollPower();
                        });
                        break;
                    case eViscaCameraCommand.PresetRecallCmd:
                        ActivePreset = _lastCalledPreset;
                        break;
                }

                _lastInquiry = eViscaCameraCommand.NoFeedback;
                readyForNextCommand();
                return;
            }

            // Message: [0x87, 0x09, 0x04, 0x00, 0xFF]
            // Vaddio heartbeat from some devices
            if (message.Length == 5 && (message[message.Length - 5] == 0x87) &&
                (message[message.Length - 4] == 0x09) &&
                (message[message.Length - 3] == 0x04) &&
                (message[message.Length - 2] == 0x00) &&
                (message[message.Length - 1] == 0xFF))
            {
                //Ignore
                return;
            }

            if (_lastInquiry != eViscaCameraCommand.NoFeedback && message.Length > 3)
            {
                switch (_lastInquiry)
                {
                    case eViscaCameraCommand.PowerInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            switch (message[message.Length - 2])
                            {
                                case 0x02:
                                    Power = true;
                                    break;
                                case 0x03:
                                case 0x04:
                                    Power = false;
                                    break;
                            }

                            _lastInquiry = eViscaCameraCommand.NoFeedback;
                            readyForNextCommand();
                        }

                        break;
                    case eViscaCameraCommand.FocusInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            switch (message[message.Length - 2])
                            {
                                case 0x02:
                                    AutoFocus = true;
                                    break;
                                case 0x03:
                                    AutoFocus = false;
                                    break;
                            }

                            _lastInquiry = eViscaCameraCommand.NoFeedback;
                            readyForNextCommand();
                        }

                        break;
                    case eViscaCameraCommand.PresetInquiry:
                        try
                        {
                            ushort preset = Convert.ToUInt16(message[message.Length - 2]);
                            Debug.Console(1, this, "Found preset feedback {0}", preset);
                            PrivacyOn = preset == _config.PrivacyOnPreset;

                            ActivePreset = preset;
                        }
                        catch
                        {
                            Debug.Console(0, this, "Exception parsing preset feedback");
                        }

                        _lastInquiry = eViscaCameraCommand.NoFeedback;
                        readyForNextCommand();
                        break;
                    case eViscaCameraCommand.TallyInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            switch (message[message.Length - 2])
                            {
                                case 0x02:
                                    TallyOn = true;
                                    break;
                                case 0x03:
                                    TallyOn = false;
                                    break;
                            }

                            _lastInquiry = eViscaCameraCommand.NoFeedback;
                            readyForNextCommand();
                        }

                        break;
                    default:
                        ParseAdditionalFeedback(message);
                        break;
                }

                return;
            }

            ParseAdditionalFeedback(message);
        }

        private void PostData(string data, eAverCameraInquiry requestName)
        {
            try
            {
                Debug.Console(1, "Aver Camera Post {0} http:{1}", requestName, data);
                HttpClientRequest req = new HttpClientRequest();
                string url = string.Format("http://{0}/{1}", hostname, data);
                string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
                req.Header.SetHeaderValue("Authorization", "Basic " + auth);
                req.Header.ContentType = "text/plain";
                req.Header.SetHeaderValue("Content-Length", "0");
                req.Encoding = Encoding.ASCII;
                req.RequestType = RequestType.Post;
                req.Url.Parse(url);

                Debug.Console(1, "Aver Camera Post to url {0} with token {1}", url, auth);
                client.DispatchAsyncEx(req, HttpCallback, requestName);
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Aver Camera Exception in PostData:{0}", ex);
            }
        }

        private void HttpCallback(HttpClientResponse response, HTTP_CALLBACK_ERROR error, object requestName)
        {
            try
            {
                if (error != HTTP_CALLBACK_ERROR.COMPLETED)
                {
                    Debug.Console(0, "Aver Camera Http client callback error: {0}", error);
                }
                else
                {
                    Debug.Console(1, "Aver Camera Http client response code:{0}", response.Code.ToString());
                    if (response.Code < 200 || response.Code >= 300)
                    {
                        Debug.Console(0, "Aver Camera Http client callback code error: {0}", response.Code);
                    }
                    else
                    {
                        Debug.Console(1, "Aver Camera Http client response content:{0}",
                            response.ContentString);
                        if (response.ContentLength > 0)
                        {
                            HttpParseMessage((eAverCameraInquiry)requestName, response.ContentString.Trim());
                        }
                        else
                        {
                            Debug.Console(0, "Aver Camera Empty http client response");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, "Aver Camera Http client callback exception: {0}", ex.Message);
            }
        }

        private void ParseAdditionalFeedback(byte[] message)
        {
            if (message.Length > 2 && message[message.Length - 2] == 0x42 &&
                message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received ack");
                if (_lastInquiry == eViscaCameraCommand.PresetRecallCmd)
                {
                    ActivePreset = _lastCalledPreset;
                }

                return;
            }

            if (message.Length > 2 && message[message.Length - 2] == 0x52 &&
                message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received execution confirmation, last inquiry: {0}", _lastInquiry.ToString());
                switch (_lastInquiry)
                {
                    case eViscaCameraCommand.PresetSave:
                        ActivePreset = _lastCalledPreset;
                        PresetSavedFb();
                        break;
                    case eViscaCameraCommand.PowerOnCmd:
                        Power = true;
                        CrestronInvoke.BeginInvoke((o) =>
                        {
                            CrestronEnvironment.Sleep(2000);
                            PollPower();
                        });
                        break;
                    case eViscaCameraCommand.PowerOffCmd:
                        Power = false;
                        CrestronInvoke.BeginInvoke((o) =>
                        {
                            CrestronEnvironment.Sleep(2000);
                            PollPower();
                        });
                        break;
                    case eViscaCameraCommand.PresetRecallCmd:
                        ActivePreset = _lastCalledPreset;
                        break;
                }

                _lastInquiry = eViscaCameraCommand.NoFeedback;
                readyForNextCommand();
            }
        }

        /// <summary>
        /// Initialize the camera by sending Address Set Broadcast and IF Clear Broadcast
        /// </summary>
        protected void InitializeCamera()
        {
            // send address set broadcast
            byte[] cmd = { 0x88, 0x30, 0x01, 0xFF };
            QueueCommand(cmd);

            // send an 'IF clear' on connection
            cmd = new byte[] { 0x88, 0x01, 0x00, 0x01, 0xFF };
            QueueCommand(cmd);
        }

        private void IFClear()
        {
            SendBytes(new byte[] { 0x88, 0x01, 0x00, 0x01, 0xFF });
        }

        /// <summary>
        /// Poll 
        /// </summary>
        public void Poll()
        {
            try
            {
                PollAutoTrack();
                switch (_pollTracker)
                {
                    case 0:
                        PollPower();
                        break;
                    case 1:
                        PollFocus();
                        break;
                    case 2:
                        PollTally();
                        break;
                }

                _pollTracker++;
                if (_pollTracker >= 3) _pollTracker = 0;
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Exception in poll command: {0}", e.Message);
            }
        }

        private void PollPower()
        {
            byte[] cmd = { _address, 0x09, 0x04, 0x00, 0xFF };
            QueueCommand(eViscaCameraCommand.PowerInquiry, cmd);
        }

        private void PollAutoTrack()
        {
            PostData("cgi-bin?Get=trk_tracking_on,3&_=X", eAverCameraInquiry.AutoTrackInquiry);
        }

        private void PollFocus()
        {
            byte[] cmd = { _address, 0x09, 0x04, 0x38, 0xFF };
            QueueCommand(eViscaCameraCommand.FocusInquiry, cmd);
        }

        private void PollTally()
        {
            byte[] cmd = { _address, 0x09, 0x7E, 0x01, 0x0A, 0xFF };
            QueueCommand(eViscaCameraCommand.TallyInquiry, cmd);
        }

        /// <summary>
        /// Set power state on
        /// </summary>
        public void SetPowerOn()
        {
            QueueCommand(eViscaCameraCommand.PowerOnCmd, new byte[] { _address, 0x01, 0x04, 0x00, 0x02, 0xFF });
        }

        /// <summary>
        /// Set power state off
        /// </summary>
        public void SetPowerOff()
        {
            QueueCommand(eViscaCameraCommand.PowerOffCmd, new byte[] { _address, 0x01, 0x04, 0x00, 0x03, 0xFF });
            ActivePreset = 0;
        }

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public void SetAutoTrackingOn()
        {
            if (AutoTrackingCapable.BoolValue)
            {
                PostData("cgi-bin?Set=trk_tracking_on,3,1", eAverCameraInquiry.AutoTrackOnCmd);
            }
        }

        /// <summary>
        /// Turn AutoTracking Off
        /// </summary>
        public void SetAutoTrackingOff()
        {
            PostData("cgi-bin?Set=trk_tracking_on,3,0", eAverCameraInquiry.AutoTrackOffCmd);
        }

        public bool OverrideAutoTracking()
        {
            if (_autoTrackingCapable && AutoTrackingOn)
            {
                SetAutoTrackingOff();
                uint count = 0;
                while (AutoTrackingOn)
                {
                    count++;
                    if (count > 30) return false;
                    CrestronEnvironment.Sleep(100);
                }
            }

            return true;
        }

        /// <summary>
        /// Move camera with automatic speed setting
        /// </summary>
        /// <param name="state">sig action true/false</param>
        /// <param name="direction">EMoveDirection direction</param>
        public void Move(bool state, EDirection direction)
        {
            if (!OverrideAutoTracking())
                return;
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
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[]
                        {
                            _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x01, 0x03, 0xFF
                        });
                    break;
                }
                case EDirection.PanRight:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[]
                        {
                            _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x02, 0x03, 0xFF
                        });
                    break;
                }
                case EDirection.TiltUp:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[]
                        {
                            _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x03, 0x01, 0xFF
                        });
                    break;
                }
                case EDirection.TiltDown:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[]
                        {
                            _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x03, 0x02, 0xFF
                        });
                    break;
                }
                case EDirection.ZoomIn:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x20 + speed), 0xFF });
                    break;
                }
                case EDirection.ZoomOut:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x30 + speed), 0xFF });
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
                case EDirection.TiltUp:
                case EDirection.TiltDown:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand,
                        new byte[] { _address, 0x01, 0x06, 0x01, 0x05, 0x05, 0x03, 0x03, 0xFF });
                    break;
                }
                case EDirection.ZoomIn:
                case EDirection.ZoomOut:
                {
                    QueueCommand(eViscaCameraCommand.PtzCommand, new byte[] { _address, 0x01, 0x04, 0x07, 0x00, 0xFF });
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
            byte[] cmd = state
                ? new byte[] { _address, 0x01, 0x04, 0x38, 0x03, 0xFF }
                : new byte[] { _address, 0x01, 0x04, 0x38, 0x02, 0xFF };
            QueueCommand(eViscaCameraCommand.AutoFocusCommand, cmd);
            PollFocus();
        }

        /// <summary>
        /// Recall Home Position
        /// </summary>
        public void RecallHomePosition()
        {
            RecallPresetByNumber(_homePreset);
        }

        protected void PresetSavedFb()
        {
            if (PresetSaved != null)
            {
                PresetSaved(this, null);
            }
        }

        /// <summary>
        /// Set the tally light to red
        /// </summary>
        public void SetTallyRed()
        {
            byte[] cmd = { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(96), 0xFF };
            QueueCommand(eViscaCameraCommand.NoFeedback, cmd);
            PollTally();
        }

        /// <summary>
        /// Set the tally light to green
        /// </summary>
        public void SetTallyGreen()
        {
            byte[] cmd = { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(98), 0xFF };
            QueueCommand(eViscaCameraCommand.NoFeedback, cmd);
            PollTally();
        }

        /// <summary>
        /// Set the tally light to off
        /// </summary>
        public void SetTallyOff()
        {
            byte[] cmd = { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(97), 0xFF };
            QueueCommand(eViscaCameraCommand.NoFeedback, cmd);
            PollTally();
        }

        /// <summary>
        /// Recall Preset by Number
        /// </summary>
        /// <param name="preset"></param>
        public void RecallPresetByNumber(uint preset)
        {
            if (preset <= 0)
                return;

            if (!OverrideAutoTracking())
                return;

            _lastCalledPreset = preset;
            byte[] cmd = { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(preset), 0xFF };
            QueueCommand(eViscaCameraCommand.PresetRecallCmd, cmd);
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
            byte[] cmd = { _address, 0x01, 0x04, 0x3F, 0x01, Convert.ToByte(preset), 0xFF };
            QueueCommand(eViscaCameraCommand.PresetSave, cmd);
        }

        public void Dispose()
        {
            if (client != null) client.Dispose();
            if (_pollTimer != null) _pollTimer.Dispose();
            if (_commandQueue != null) _commandQueue.Dispose();
            if (_commandMutex != null) _commandMutex.Dispose();
            if (_commandTimer != null) _commandTimer.Dispose();
            if (_feedbackMutex != null) _feedbackMutex.Dispose();
        }
    }
}