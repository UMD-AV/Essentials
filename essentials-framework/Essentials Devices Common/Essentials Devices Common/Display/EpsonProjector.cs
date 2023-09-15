using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Routing;
using Newtonsoft.Json;
using Feedback = PepperDash.Essentials.Core.Feedback;

using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Devices.Displays
{
	/// <summary>
	/// 
	/// </summary>
    public class EpsonProjector : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced, IBasicVolumeWithFeedback, IHasLampHours, IHasErrorString
	{
		public IBasicCommunication Communication { get; private set; }				
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        public IntFeedback LampHoursFeedback { get; private set; }
        public BoolFeedback VideoMuteIsOnFeedback { get; private set; }
        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }
        public BoolFeedback Input4Feedback { get; private set; }
        public StringFeedback ErrorFeedback { get; private set; }

        byte[] _tcpHandshake = new byte[] { 0x45, 0x53, 0x43, 0x2F, 0x56, 0x50, 0x2E, 0x6E, 0x65, 0x74, 0x10, 0x03, 0x00, 0x00, 0x00, 0x00 };
        byte[] _tcpHeader = new byte[] { 0x45, 0x53, 0x43, 0x2F, 0x56, 0x50, 0x2E, 0x6E, 0x65, 0x74, 0x10, 0x03, 0x00, 0x00 };
        bool _readyForCommands;
        bool _readyForNextCommand;
        bool _tcpComm;
        ushort _pollTracker;
		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
        bool _VideoMuteIsOn;
        bool _abnormalStandby;
        int _LampHours;
        int _CurrentInputIndex;
        ushort _onRetryCount;
        ushort _offRetryCount;
        ushort _RequestedPowerState; // 0:none 1:on 2:off
        ushort _RequestedInputState; // 0:none 1-4:inputs 1-4 
        ushort _RequestedVideoMuteState; // 0:none 1:on 2:off
        ushort? _RequestedVolume;
        string _errorFeedback;
        public string ErrorFb
        {
            get { return _errorFeedback; }
            set
            {
                if (_errorFeedback == value) return;
                _errorFeedback = value;
                ErrorFeedback.FireUpdate();
            }
        }

        readonly EpsonQueue _cmdQueue;
        readonly EpsonQueue _priorityQueue;
        readonly EpsonQueue _volumeQueue;
        CommunicationGather _PortGather;
        RoutingInputPort _CurrentInputPort;
        CMutex _CommandMutex;
        CMutex _PowerMutex;

        //Volume Stuff
        private CCriticalSection _rampLock;
        private eRampDirection _rampDirection;
        private bool _isMuted;
        private ushort _savedVolumeForMute;
        private ushort _lastVolumeFb;
        private ushort _defaultVolume;
        private ushort _volumeSteps;
        private ushort _upperLimit;
        private ushort _lowerLimit;
        public bool MuteFb
        {
            get { return _isMuted; }
            set
            {
                _isMuted = value;
                MuteFeedback.FireUpdate();
            }
        }
        public ushort VolumeFb
        {
            get { return _lastVolumeFb; }
            set
            {
                _lastVolumeFb = value;
                if(value > 0)
                {
                    MuteFb = false;
                }
                else
                {
                    MuteFb = true;
                }
                VolumeLevelFeedback.FireUpdate();
            }
        }

		protected override Func<bool> PowerIsOnFeedbackFunc { get { return () => _PowerIsOn; } }
        protected override Func<bool> IsWarmingUpFeedbackFunc { get { return () => _IsWarmingUp; } }
		protected override Func<bool> IsCoolingDownFeedbackFunc { get { return () => _IsCoolingDown; } }
        protected override Func<string> CurrentInputFeedbackFunc { get { return () => _CurrentInputPort.Key; } }
 

		/// <summary>
		/// Constructor for IBasicCommunication
		/// </summary>
		public EpsonProjector(string key, string name, EpsonProjectorPropertiesConfig config, IBasicCommunication comm)
			: base(key, name)
		{
			Communication = comm;
            Communication.BytesReceived += new EventHandler<GenericCommMethodReceiveBytesArgs>(BytesReceived);
            _PortGather = new CommunicationGather(Communication, '\x0D');
            _PortGather.IncludeDelimiter = false;
            _PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(DelimitedTextReceived);

            var tcpComm = comm as GenericTcpIpClient;
            _readyForCommands = false;
            if (tcpComm != null)
            {
                _tcpComm = true;
                tcpComm.AutoReconnect = true;
                tcpComm.AutoReconnectIntervalMs = 10000;
                tcpComm.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(tcpComm_ConnectionChange);
            }
            else
            {
                _tcpComm = false;
            }

            _cmdQueue = new EpsonQueue();
            _priorityQueue = new EpsonQueue();
            _volumeQueue = new EpsonQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();
            _readyForNextCommand = true;

            LampHoursFeedback = new IntFeedback(() => _LampHours);
            VideoMuteIsOnFeedback = new BoolFeedback(() => _VideoMuteIsOn);
            Input1Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 1; });
            Input2Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 2; });
            Input3Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 3; });
            Input4Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 4; });
            ErrorFeedback = new StringFeedback(() => _errorFeedback);

            _onRetryCount = 0;
            _offRetryCount = 0;
            _pollTracker = 0;
            _LampHours = 0;
            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            _RequestedVideoMuteState = 0;
            _errorFeedback = "";
            WarmupTime = 60000;
            CooldownTime = 30000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

            _defaultVolume = (ushort)(config.defaultVolume == null ? 10 : config.defaultVolume);
            _RequestedVolume = null;
            _volumeSteps = (ushort)(config.volumeSteps == null ? 20 : config.volumeSteps);
            _upperLimit = (ushort)(config.volumeUpperLimit == null ? 255 : config.volumeUpperLimit);
            _lowerLimit = (ushort)(config.volumeLowerLimit == null ? 0 : config.volumeLowerLimit);
            InitVolumeControls();

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            AddRoutingInputPort(new RoutingInputPort("HDMI 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "30");

            AddRoutingInputPort(new RoutingInputPort("HDMI 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "A0");

            AddRoutingInputPort(new RoutingInputPort("HdBaseT", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DmCat, new Action(InputNetwork), this), "80");

            AddRoutingInputPort(new RoutingInputPort("VGA", eRoutingSignalType.Video,
                eRoutingPortConnectionType.Vga, new Action(InputVga), this), "11");
		}

        void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            if (!_tcpComm)
            {
                _readyForCommands = true;
            }

            CommunicationMonitor.StatusChange += (o, a) => Debug.Console(1, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
            CommunicationMonitor.Start();
            return true;
        }

	    public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
	    {
            LinkDisplayToApi(this, trilist, joinStart, joinMapKey, bridge);
            var joinMap = new EpsonProjectorJoinMap(joinStart);

            trilist.SetSigTrueAction(joinMap.VideoMuteOn.JoinNumber, VideoMuteOn);
            trilist.SetSigTrueAction(joinMap.VideoMuteOff.JoinNumber, VideoMuteOff);
            trilist.BooleanInput[joinMap.LampHoursSupported.JoinNumber].BoolValue = true;
            trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = true;

            IsWarmingUpFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Warming.JoinNumber]);
            IsCoolingDownFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Cooling.JoinNumber]);
            VideoMuteIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VideoMuteOn.JoinNumber]);
            LampHoursFeedback.LinkInputSig(trilist.UShortInput[joinMap.LampHours.JoinNumber]);
            Input1Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 0]);
            Input2Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 1]);
            Input3Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 2]);
            Input4Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 3]);
            ErrorFeedback.LinkInputSig(trilist.StringInput[joinMap.ErrorMessage.JoinNumber]);

            // Volume
            trilist.SetSigTrueAction(joinMap.DefaultVolume.JoinNumber, DefaultVolume);
            trilist.SetBoolSigAction(joinMap.VolumeUp.JoinNumber, VolumeUp);
            trilist.SetBoolSigAction(joinMap.VolumeDown.JoinNumber, VolumeDown);
            trilist.SetSigTrueAction(joinMap.VolumeMute.JoinNumber, MuteToggle);
            trilist.SetSigTrueAction(joinMap.VolumeMuteOn.JoinNumber, MuteOn);
            trilist.SetSigTrueAction(joinMap.VolumeMuteOff.JoinNumber, MuteOff);
            trilist.SetUShortSigAction(joinMap.VolumeLevel.JoinNumber, SetVolume);
            VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.VolumeLevel.JoinNumber]);
            MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VolumeMuteOn.JoinNumber]);
            MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VolumeMute.JoinNumber]);
            MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.VolumeMuteOff.JoinNumber]);
	    }

        private void InitVolumeControls()
        {
            VolumeLevelFeedback = new IntFeedback(() => VolumeFb);
            MuteFeedback = new BoolFeedback(() => MuteFb);
            _rampLock = new CCriticalSection();
        }

        void tcpComm_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (!e.Client.IsConnected)
            {
                _readyForCommands = false;
                _cmdQueue.ClearQueue();
                _priorityQueue.ClearQueue();
                _volumeQueue.ClearQueue();
                _CurrentInputIndex = 0;
            }
        }

        void BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            if(!_readyForCommands)
            {
                try
                {
                    Debug.Console(1, this, "EpsonProjector BytesReceived: {0}", ComTextHelper.GetEscapedText(e.Bytes));
                    //Try to trim any beginning garbage
                    for (int startPos = 0; (startPos + _tcpHeader.Length) < e.Bytes.Length; startPos++)
                    {
                        int headerPos = 0;
                        while (e.Bytes[startPos + headerPos] == _tcpHeader[headerPos])
                        {
                            //Iterate through byte array looking for a match to the tcp header byte array
                            headerPos++;
                            if (headerPos == _tcpHeader.Length)
                            {
                                //found header match
                                byte statusCode = e.Bytes[startPos + headerPos];
                                Debug.Console(1, this, "EpsonProjector tcp status code: {0}", string.Format(@"[{0:X2}]", (int)statusCode));
                                if (statusCode == 0x20)
                                {
                                    Debug.Console(0, this, "EpsonProjector connected");
                                    _readyForCommands = true;
                                    Resync();
                                }
                                else
                                {
                                    Debug.Console(0, this, "EpsonProjector password required");
                                }
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, this, "Error parsing BytesReceived: {0}", ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        void DelimitedTextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            try
            {
                string feedback = e.Text.Replace(":", "").Trim();
                Debug.Console(1, this, "Feedback: {0}", feedback);
                _readyForNextCommand = true;

                if (feedback == "ERR")
                {
                    if (!_readyForCommands)
                    {
                        _readyForCommands = true;
                        Resync();
                    }
                    return;
                }

                string[] data = feedback.Split('=');

                if(data.Length > 1)
                {
                    if (data[0].EndsWith("PWR"))
                    {
                        if (data[1] == "01")
                        {
                            _onRetryCount = 0;
                            //Update power on feedback
                            if (_PowerIsOn == false)
                            {
                                _PowerIsOn = true;                                
                                PowerIsOnFeedback.FireUpdate();
                            }
                            
                            //Clear power check
                            _PowerMutex.WaitForMutex();
                            if (_RequestedPowerState == 1)
                            {
                                _RequestedPowerState = 0;
                            }
                            _PowerMutex.ReleaseMutex();

                            //Clear any abnormal standby
                            if (_abnormalStandby)
                            {
                                _abnormalStandby = false;
                                ErrorFb = "";
                            }

                            //Finish warming up process
                            if (_IsWarmingUp)
                            {
                                CrestronInvoke.BeginInvoke((o) => WarmupDone());
                            }
                        }
                        else if (data[1] == "02")
                        {
                            if (!_IsWarmingUp)
                            {
                                CrestronInvoke.BeginInvoke((o) => WarmupStart());
                            }
                            else
                            {
                                WarmupTimer.Reset(WarmupTime);
                            }
                        }
                        else if (data[1] == "03")
                        {
                            if (!_IsCoolingDown)
                            {
                                CrestronInvoke.BeginInvoke((o) => CooldownStart());
                            }
                            else
                            {
                                CooldownTimer.Reset(CooldownTime);
                            }
                        }
                        else if (data[1] == "00" || data[1] == "04" || data[1] == "09")
                        {
                            _offRetryCount = 0;
                            //Update power on feedback
                            if (_PowerIsOn == true)
                            {
                                _PowerIsOn = false;
                                _VideoMuteIsOn = false;
                                PowerIsOnFeedback.FireUpdate();
                                VideoMuteIsOnFeedback.FireUpdate();
                            }

                            //Clear power check
                            _PowerMutex.WaitForMutex();
                            if (_RequestedPowerState == 2)
                            {
                                _RequestedPowerState = 0;
                            }
                            _PowerMutex.ReleaseMutex();

                            //Clear any abnormal standby
                            if (_abnormalStandby)
                            {
                                _abnormalStandby = false;
                                ErrorFb = "";
                            }

                            //Finish cooling down process
                            if (_IsCoolingDown)
                            {
                                CrestronInvoke.BeginInvoke((o) =>CooldownDone());
                            }
                        }
                        else if (data[1] == "05")
                        {
                            //Abnormal standby - only when projector has issues
                            if (!_abnormalStandby)
                            {
                                _abnormalStandby = true;
                                ErrorFb = "Projector in abnormal standby";
                                Debug.LogError(Debug.ErrorLogLevel.Warning, "Projector in abnormal standby");
                            }

                            _offRetryCount = 0;
                            //Update power on feedback
                            if (_PowerIsOn == true)
                            {
                                _PowerIsOn = false;
                                PowerIsOnFeedback.FireUpdate();
                            }

                            //Clear power check
                            _PowerMutex.WaitForMutex();
                            _RequestedPowerState = 0;
                            _PowerMutex.ReleaseMutex();

                            //Finish cooling down process
                            if (_IsCoolingDown)
                            {
                                CrestronInvoke.BeginInvoke((o) => CooldownDone());
                            }
                        }
                    }
                    else if (data[0].EndsWith("SOURCE"))
                    {
                        try
                        {
                            int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(data[1]));
                            var newInput = InputPorts[index];
                            if (_CurrentInputIndex != (index + 1))
                            {
                                _CurrentInputIndex = index + 1; //Offset from 0 based index
                            }
                            if (_CurrentInputIndex == _RequestedInputState)
                            {
                                _RequestedInputState = 0;
                            }
                            if (newInput != null && newInput != _CurrentInputPort)
                            {
                                _CurrentInputPort = newInput;
                                CurrentInputFeedback.FireUpdate();
                                OnSwitchChange(new RoutingNumericEventArgs(null, _CurrentInputPort, eRoutingSignalType.AudioVideo));
                                VolumeGet();
                            }
                            Input1Feedback.FireUpdate();
                            Input2Feedback.FireUpdate();
                            Input3Feedback.FireUpdate();
                            Input4Feedback.FireUpdate();
                        }
                        catch
                        {
                            Debug.Console(1, this, "Invalid input feedback: {0}", data[1]);
                        }
                    }
                    else if (data[0].EndsWith("LAMP"))
                    {
                        int newHours = int.Parse(data[1]);

                        if(_LampHours != newHours)
                        {
                            _LampHours = newHours;
                            LampHoursFeedback.FireUpdate();
                        }
                    }
                    else if (data[0].EndsWith("MUTE"))
                    {
                        
                        if (data[1] == "ON")
                        {
                            _RequestedVideoMuteState = 0;
                            _VideoMuteIsOn = true;
                            VideoMuteIsOnFeedback.FireUpdate();
                        }
                        else if (data[1] == "OFF")
                        {
                            _RequestedVideoMuteState = 0;
                            _VideoMuteIsOn = false;
                            VideoMuteIsOnFeedback.FireUpdate();
                            ResyncPowerOnState();
                        }
                    }
                    else if (data[0].EndsWith("VOL"))
                    {
                        ushort newVol = ushort.Parse(data[1]);
                        if (newVol > 0)
                        {
                            _savedVolumeForMute = newVol;
                        }
                        ushort scaledVol = GetScaledVolumeFb(newVol);

                        Debug.Console(0, this, "Requested volume: {0} feedback volume {1}", _RequestedVolume, newVol);
                        if (_RequestedVolume == newVol)
                        {                            
                            _RequestedVolume = null;
                        }
                        VolumeFb = scaledVol;
                    }
                    else if (data[0].EndsWith("ERR"))
                    {
                        if (_abnormalStandby)
                            return;
                        string error = data[1];
                        switch (error)
                        {
                            case "00":
                                ErrorFb = "";
                                break;
                            case "01":
                                ErrorFb = "Fan error";
                                break;
                            case "03":
                                ErrorFb = "Lamp failure at power on";
                                break;
                            case "04":
                                ErrorFb = "High internal temperature error";
                                break;
                            case "06":
                                ErrorFb = "Lamp error";
                                break;
                            case "07":
                                ErrorFb = "Open lamp cover door error";
                                break;
                            case "08":
                                ErrorFb = "Cinema filter error";
                                break;
                            case "09":
                                ErrorFb = "Electric dual-layered capacitor is disconnected";
                                break;
                            case "0A":
                                ErrorFb = "Auto iris error";
                                break;
                            case "0B":
                                ErrorFb = "Subsystem error";
                                break;
                            case "0C":
                                ErrorFb = "Low air flow error";
                                break;
                            case "0D":
                                ErrorFb = "Air filter air flow sensor error";
                                break;
                            case "0E":
                                ErrorFb = "Power supply unit error (Ballast)";
                                break;
                            case "0F":
                                ErrorFb = "Shutter error";
                                break;
                            case "10":
                                ErrorFb = "Cooling system error (peltiert element)";
                                break;
                            case "11":
                                ErrorFb = "Cooling system error (Pump)";
                                break;
                            default:
                                ErrorFb = "Unknown error";
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }
        }

        private void Resync()
        {
            StatusGet();
            ProcessPower();
            if (!_IsCoolingDown && !_IsWarmingUp)
            {
                if (_RequestedVideoMuteState == 1)
                {
                    VideoMuteOnGo();
                }
                else
                {
                    ResyncPowerOnState();
                }
            } 
        }

        private void ResyncPowerOnState()
        {
            if (_RequestedVideoMuteState == 2)
            {
                VideoMuteOffGo();
            }
            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }
            if (_RequestedVolume != null)
            {
                SetVolumeRaw((ushort)_RequestedVolume);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendCommand(eCommandType type, string cmd, bool priority)
        {
            if (_readyForCommands)
            {
                var kvp = new KeyValuePair<eCommandType, string>(type, cmd);
                Debug.Console(1, this, "Enqueuing command: {0}", cmd);
                if (type == eCommandType.Volume)
                {
                    if (_volumeQueue.Count > 5)
                    {
                        _volumeQueue.ClearQueue();
                    }
                    _volumeQueue.AddCommand(kvp);
                }
                else if (priority)
                {                    
                    _priorityQueue.AddOrUpdateCommand(kvp);
                }
                else
                {
                    _cmdQueue.AddOrUpdateCommand(kvp);
                }
                CrestronInvoke.BeginInvoke((o) => ProcessQueue());
            }
            else
            {
                Debug.Console(1, this, "EpsonProjector not connected, ignoring command");
            }
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (test)
            {
                //Pace the commands sending out
                while (_volumeQueue.Count > 0 || _cmdQueue.Count > 0 || _priorityQueue.Count > 0)
                {
                    try
                    {
                        KeyValuePair<eCommandType, string> kvp;
                        if (_volumeQueue.Count > 0)
                        {
                            while (_volumeQueue.Count > 0)
                            {
                                kvp = _volumeQueue.Dequeue();
                                Debug.Console(1, this, "Sending Text: {0}", kvp.Value);
                                Communication.SendText(kvp.Value + "\x0D");
                                Thread.Sleep(100);
                                Communication.SendText("VOL?\x0D");
                            }
                            kvp = new KeyValuePair<eCommandType, string>();
                        }
                        else if (_priorityQueue.Count > 0)
                        {
                            kvp = _priorityQueue.Dequeue();
                        }
                        else
                        {
                            kvp = _cmdQueue.Dequeue();
                        }
                        if (kvp.Value != null)
                        {
                            _readyForNextCommand = false;
                            Debug.Console(1, this, "Sending Text: {0}", kvp.Value);
                            Communication.SendText(kvp.Value + "\x0D");

                            if (kvp.Key == eCommandType.VideoMute)
                            {
                                Thread.Sleep(500);
                                _readyForNextCommand = true;
                            }
                            if (!Communication.IsConnected)
                            {
                                //Fail safe for no feedback
                                Thread.Sleep(500);
                            }
                            else
                            {
                                int count = 0;
                                while (!_readyForNextCommand && count < 100)
                                {
                                    Thread.Sleep(20);
                                    count++;
                                }
                                if (count >= 100)
                                {
                                    if (!_IsWarmingUp && !_IsCoolingDown)
                                    {
                                        Debug.Console(0, this, "ProcessQueue timed out waiting for next command. Last command: {0}", kvp.Value);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Caught an exception in ProcessQueue {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                    }
                }
                _CommandMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void StatusGet()
        {
            if (_readyForCommands)
            {
                PowerGet();

                if (_PowerIsOn && !_IsWarmingUp)
                {
                    //Only poll these while projector is warmed up and on, otherwise it responds "ERR"
                    VideoMuteGet();
                    InputGet();
                    VolumeGet();
                }
                if (_pollTracker >= 7)
                {
                    LampHoursGet();
                    ErrorGet();
                    _pollTracker = 0;
                }
                _pollTracker++;
            }
            else if(Communication.IsConnected)
            {
                Debug.Console(1, this, "Sending tcp handshake");
                Communication.SendBytes(_tcpHandshake);
                Thread.Sleep(500);
                if (!_readyForCommands)
                {
                    //Try to get response by sending return char
                    Communication.SendText("\x0D");
                }
            }
        }

        private void WarmupCallback(object o)
        {
            Debug.Console(0, this, "Warmup timed out");
            _onRetryCount++;
            _PowerMutex.WaitForMutex();
            if (!CommunicationMonitor.IsOnline || (_RequestedPowerState == 1 && _onRetryCount > 2))
            {
                _RequestedPowerState = 0;
                Debug.LogError(Debug.ErrorLogLevel.Warning, "Projector warmup timed out");
            }
            _PowerMutex.ReleaseMutex();
            WarmupDone();
        }

        private void WarmupStart()
        {
            if (!_IsWarmingUp)
            {
                CooldownTimer.Stop();
                _PowerIsOn = true;
                _IsWarmingUp = true;
                _IsCoolingDown = false;
                IsWarmingUpFeedback.FireUpdate();
                IsCoolingDownFeedback.FireUpdate();
                PowerIsOnFeedback.FireUpdate();
                WarmupTimer.Reset(WarmupTime);
                while (_IsWarmingUp)
                {
                    SendCommand(eCommandType.PowerPoll, "PWR?", true);
                    if (_RequestedVideoMuteState == 1)
                    {
                        VideoMuteOnGo();
                    }
                    else
                    {
                        ResyncPowerOnState();
                    }
                    Thread.Sleep(2000);
                }
            }
        }

        private void WarmupDone()
        {
            WarmupTimer.Stop();
            _PowerMutex.WaitForMutex();
            _PowerMutex.ReleaseMutex();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

            VideoMuteGet();
            InputGet();
            VolumeGet();

            ProcessPower();

            if (_RequestedVideoMuteState == 1)
            {
                VideoMuteOnGo();
            }
            else
            {
                ResyncPowerOnState();
            }
        }

        private void CooldownCallback(object o)
        {
            Debug.Console(0, this, "Cooldown timed out");
            _offRetryCount++;
            _PowerMutex.WaitForMutex();
            if (!CommunicationMonitor.IsOnline || (_RequestedPowerState == 2 && _offRetryCount > 2))
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning, "Projector cooldown timed out");
                _RequestedPowerState = 0;
            }
            _PowerMutex.ReleaseMutex();
            CooldownDone();
        }

        private void CooldownStart()
        {
            if (!_IsCoolingDown)
            {
                WarmupTimer.Stop();
                _PowerIsOn = false;
                PowerIsOnFeedback.FireUpdate();
                _IsCoolingDown = true;
                _IsWarmingUp = false;
                IsWarmingUpFeedback.FireUpdate();
                IsCoolingDownFeedback.FireUpdate();
                CooldownTimer.Reset(CooldownTime);
                while (_IsCoolingDown)
                {
                    SendCommand(eCommandType.PowerPoll, "PWR?", true);
                    Thread.Sleep(2000);
                }
            }
        }

        private void CooldownDone()
        {
            CooldownTimer.Stop();
            _PowerMutex.WaitForMutex();
            _PowerMutex.ReleaseMutex();
            _IsWarmingUp = false;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            _VideoMuteIsOn = false;
            VideoMuteIsOnFeedback.FireUpdate();

            ProcessPower();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PowerOn()
		{
            _PowerMutex.WaitForMutex();
            _RequestedPowerState = 1;
            _PowerMutex.ReleaseMutex();
            ProcessPower();
		}

        /// <summary>
        /// 
        /// </summary>
		public override void PowerOff()
		{
            _PowerMutex.WaitForMutex();
            _RequestedPowerState = 2;
            _PowerMutex.ReleaseMutex();
            _RequestedInputState = 0;
            _RequestedVideoMuteState = 0;
            _RequestedVolume = null;
            ProcessPower();
		}

        private void PowerOnGo()
        {
            SendCommand(eCommandType.Power, "PWR ON", true);
            CrestronInvoke.BeginInvoke((o) => WarmupStart());
        }

        private void PowerOffGo()
        {
            SendCommand(eCommandType.Power, "PWR OFF", true);
            CrestronInvoke.BeginInvoke((o) => CooldownStart());
        }

        private void ProcessPower()
        {
            if (!_IsWarmingUp && !_IsCoolingDown)
            {
                if (_RequestedPowerState == 1 && (_PowerIsOn == false || !CommunicationMonitor.IsOnline))
                {
                    PowerOnGo();
                }
                else if (_RequestedPowerState == 2 && (_PowerIsOn == true || !CommunicationMonitor.IsOnline))
                {
                    PowerOffGo();
                }
            }
        }
		
		public override void PowerToggle()
		{
            if (_PowerIsOn)
            {
                PowerOff();
            }
            else
            {
                PowerOn();
            }
		}

        public void PowerGet()
        {
            SendCommand(eCommandType.PowerPoll, "PWR?", false);
        }

        public void VideoMuteOn()
        {
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                _RequestedVideoMuteState = 1;
                if (!_IsWarmingUp)
                {
                    VideoMuteOnGo();
                }
            }
        }

        private void VideoMuteOnGo()
        {
            SendCommand(eCommandType.VideoMute, "MUTE ON", false);
            VideoMuteGet();
            CrestronInvoke.BeginInvoke((o) => {
                Thread.Sleep(1000);
                VideoMuteGet();
            });
        }

        public void VideoMuteOff()
        {
            _RequestedVideoMuteState = 2;
            if (_PowerIsOn && !_IsWarmingUp)
            {
                VideoMuteOffGo();
            }
        }

        private void VideoMuteOffGo()
        {
            SendCommand(eCommandType.VideoMute, "MUTE OFF", false);
            VideoMuteGet();
            CrestronInvoke.BeginInvoke((o) =>
            {
                Thread.Sleep(1000);
                VideoMuteGet();
            });
        }

        public void VideoMuteGet()
        {
            SendCommand(eCommandType.VideoMutePoll, "MUTE?", false);
        }

        public void LampHoursGet()
        {
            SendCommand(eCommandType.LampPoll, "LAMP?", false);
        }

        public void ErrorGet()
        {
            SendCommand(eCommandType.ErrorPoll, "ERR?", false);
        }

        public void VolumeGet()
        {
            SendCommand(eCommandType.VolumePoll, "VOL?", false);
        }

        public void InputSelect(ushort input)
        {
            switch (input)
            {
                case 1:
                    InputHdmi1();
                    break;
                case 2:
                    InputHdmi2();
                    break;
                case 3:
                    InputNetwork();
                    break;
                case 4:
                    InputVga();
                    break;
            }
        }

        private void InputSelectGo(ushort input)
        {
            switch (input)
            {
                case 1:
                    InputHdmi1Go();
                    break;
                case 2:
                    InputHdmi2Go();
                    break;
                case 3:
                    InputNetworkGo();
                    break;
                case 4:
                    InputVgaGo();
                    break;
            }
        }

		public void InputHdmi1()
		{
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                RequestInput(1);
                if (!_IsWarmingUp)
                {
                    InputHdmi1Go();
                }
            }
		}

        public void InputHdmi2()
        {
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                RequestInput(2);
                if (!_IsWarmingUp)
                {
                    InputHdmi2Go();
                }
            }
        }

        public void InputNetwork()
        {
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                RequestInput(3);
                 if (!_IsWarmingUp)
                {
                    InputNetworkGo();
                }   
            }
        }

        public void InputVga()
        {
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                RequestInput(4);
                if (!_IsWarmingUp)
                {
                    InputVgaGo();
                }
            }
        }

        private void RequestInput(ushort input)
        {
            _RequestedInputState = input;
            CrestronInvoke.BeginInvoke(o =>
            {
                switch (_RequestedInputState)
                {
                    case 1:
                        InputHdmi1Go();
                        break;
                    case 2:
                        InputHdmi2Go();
                        break;
                    case 3:
                        InputNetworkGo();
                        break;
                    case 4:
                        InputVgaGo();
                        break;
                }
            });
        }

        private void InputHdmi1Go()
        {
            if (_CurrentInputIndex != 1 && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                _CurrentInputIndex = 0;
                SendCommand(eCommandType.Input, "SOURCE 30", false);
                InputGet();
                VideoMuteGet();
            }
        }

        public void InputHdmi2Go()
        {
            if (_CurrentInputIndex != 2 && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                _CurrentInputIndex = 0;
                SendCommand(eCommandType.Input, "SOURCE A0", false);
                InputGet();
                VideoMuteGet();
            }
        }

		public void InputNetworkGo()
		{
            if (_CurrentInputIndex != 3 && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                _CurrentInputIndex = 0;
                SendCommand(eCommandType.Input, "SOURCE 80", false);
                InputGet();
                VideoMuteGet();
            }
        }

        public void InputVgaGo()
        {
            if (_CurrentInputIndex != 4 && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                _CurrentInputIndex = 0;
                SendCommand(eCommandType.Input, "SOURCE 11", false);
                InputGet();
                VideoMuteGet();
            }
        }

        public void InputGet()
        {
            SendCommand(eCommandType.InputPoll, "SOURCE?", false);
        }

        /// <summary>
        /// Executes a switch.
        /// </summary>
        /// <param name="selector"></param>
		public override void ExecuteSwitch(object selector)
		{
            (selector as Action)();
		}

        public enum eCommandType
        {
            Power,
            Input,
            VideoMute,
            PowerPoll,
            InputPoll,
            VideoMutePoll,
            LampPoll,
            ErrorPoll,
            Volume,
            VolumePoll
        }

        private class EpsonQueue
        {
            public List<KeyValuePair<eCommandType, string>> Q = new List<KeyValuePair<eCommandType, string>>();
            public ushort Count { get { return (ushort)Q.Count; } }
            private CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing Epson Projector commands
            /// </summary>
            public EpsonQueue()
            {
            }

            public void AddCommand(KeyValuePair<eCommandType, string> command)
            {
                mutex.WaitForMutex();
                try
                {
                    Q.Add(command);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Epson command queue add: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public void AddOrUpdateCommand(KeyValuePair<eCommandType, string> command)
            {
                mutex.WaitForMutex();
                try
                {
                    int i = Q.FindIndex(x => x.Key.Equals(command.Key));
                    if (i != -1 && (command.Key == eCommandType.Input || command.Key == eCommandType.VideoMute))
                    {
                        Q[i] = command;                        
                    }
                    else if (i != -1 && (command.Key == eCommandType.InputPoll || command.Key == eCommandType.LampPoll || command.Key == eCommandType.ErrorPoll ||
                        command.Key == eCommandType.PowerPoll || command.Key == eCommandType.VideoMutePoll || command.Key == eCommandType.VolumePoll))
                    {
                        //Move poll to end of queue
                        Q.RemoveAt(i);
                        Q.Add(command);
                    }
                    else
                    {
                        Q.Add(command);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Epson command queue add/update: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public void ClearQueue()
            {
                mutex.WaitForMutex();
                try
                {
                    Q.Clear();
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Epson command queue clear: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public KeyValuePair<eCommandType, string> Dequeue()
            {
                KeyValuePair<eCommandType, string> kvp = new KeyValuePair<eCommandType, string>();
                mutex.WaitForMutex();
                try
                {
                    if (Q.Count > 0)
                    {
                        kvp = Q[0];
                        Q.RemoveAt(0);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Epson command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
                return kvp;
            }
        }

        #region IBasicVolumeWithFeedback Members

        /// <summary>
        /// Scales the 16 bit level to the range of the display and sends the command
        /// </summary>
        /// <param name="level"></param>
        public void SetVolume(ushort level)
        {
            //Scale volume from Crestron 16 bit to configurable volume range
            var scaled = Math.Round((double)(NumericalHelpers.Scale(level, 0, 65535, _lowerLimit, _upperLimit)));
            if (scaled > 0)
            {
                SetVolumeRaw((ushort)scaled);
            }
        }

        /// <summary>
        /// Scales the raw level to the range of the display and sends the command
        /// </summary>
        /// <param name="level"></param>
        private void SetVolumeScaled(ushort level)
        {
            //Convert to 8 bit based on Epson model. Different models have different volume ranges but typically 0-20 (21 steps) - see API doc and set via "volumeSteps" config value.
            var scaled = Math.Floor((double)(level * 256 / _volumeSteps));
            SetVolumeRaw((ushort)scaled);
        }

        /// <summary>
        /// Sends a raw volume command to the display
        /// </summary>
        /// <param name="level"></param>
        private void SetVolumeRaw(ushort level)
        {
            if (_RequestedPowerState == 1 || _PowerIsOn)
            {
                _RequestedVolume = level;
                if (_RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
                {
                    Debug.Console(1, this, "Setting volume to raw level: {0}", level);
                    SendCommand(eCommandType.Volume, string.Format("VOL {0}", level), true);
                    VolumeGet();
                }
            }
        }

        public ushort GetScaledVolumeFb(int level)
        {
            //First convert from Epson 8 bit to model adjusted volume factor
            var scaled = Math.Round((double)level * _volumeSteps / 256);

            if (scaled >= _upperLimit)
                return 65535;
            if (scaled <= _lowerLimit)
                return 0;
            else
            {
                //Scale volume feedback (Epson range is 0-255, but we can limit range further) to Crestron 16 bit
                return (ushort)NumericalHelpers.Scale(scaled, _lowerLimit, _upperLimit, 0, 65535);
            }
        }

        public void DefaultVolume()
        {
            SetVolumeScaled(_defaultVolume);
        }

        /// <summary>
        /// Volume level feedback property
        /// </summary>
        public IntFeedback VolumeLevelFeedback { get; private set; }

        /// <summary>
        /// volume mte feedback property
        /// </summary>
        public BoolFeedback MuteFeedback { get; private set; }

        public void MuteOff()
        {
            if (_RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                if (_savedVolumeForMute > 0)
                {
                    SetVolumeRaw(_savedVolumeForMute);
                }
                else
                {
                    DefaultVolume();
                }
            }
        }

        public void MuteOn()
        {
            if (_RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
            {
                _RequestedVolume = null;
                SendCommand(eCommandType.Volume, "VOL 0", false);
            }
        }

        /// <summary>
        /// Mute toggle
        /// </summary>
        public void MuteToggle()
        {
            if (_isMuted)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        public enum eRampDirection
        {
            Up,
            Down,
            Stop
        }

        /// <summary>
        /// Volume down (decrement)
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeDown(bool pressRelease)
        {
            _rampDirection = pressRelease ? eRampDirection.Down : eRampDirection.Stop;

            if (pressRelease)
            {
                CrestronInvoke.BeginInvoke((o) => VolumeDownLoop());
            }
        }

        private void VolumeDownLoop()
        {
            try
            {
                _rampLock.Enter();
                _RequestedVolume = null;
                while (_rampDirection == eRampDirection.Down && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
                {                    
                    SendCommand(eCommandType.Volume, "VOL DEC", false);
                    VolumeGet();
                    Thread.Sleep(200);
                }
            }
            finally
            {
                _rampLock.Leave();
            }
        }

        /// <summary>
        /// Volume up (increment)
        /// </summary>
        /// <param name="pressRelease"></param>
        public void VolumeUp(bool pressRelease)
        {
            _rampDirection = pressRelease ? eRampDirection.Up : eRampDirection.Stop;

            if (pressRelease)
            {
                CrestronInvoke.BeginInvoke((o) => VolumeUpLoop());
            }
        }

        private void VolumeUpLoop()
        {
            try
            {
                _rampLock.Enter();
                _RequestedVolume = null;
                while (_rampDirection == eRampDirection.Up && _RequestedVideoMuteState != 1 && !_VideoMuteIsOn)
                {                    
                    SendCommand(eCommandType.Volume, "VOL INC", false);
                    VolumeGet();
                    Thread.Sleep(200);
                }
            }
            finally
            {
                _rampLock.Leave();
            }
        }

        #endregion
	}

    public class EpsonProjectorJoinMap : DisplayControllerJoinMap
    {
        [JoinName("Warming")]
        public JoinDataComplete Warming = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 53,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Warming"
            });

        [JoinName("Cooling")]
        public JoinDataComplete Cooling = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 54,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Cooling"
            });

        [JoinName("Video Mute On")]
        public JoinDataComplete VideoMuteOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 57,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Video Mute On"
            });

        [JoinName("Video Mute Off")]
        public JoinDataComplete VideoMuteOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 58,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Video Mute Off"
            });

        [JoinName("Video Mute Supported")]
        public JoinDataComplete VideoMuteSupported = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 55,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Video Mute Supported"
            });

        [JoinName("Lamp Hours Supported")]
        public JoinDataComplete LampHoursSupported = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 56,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Label = "Lamp Hours Supported"
            });

        [JoinName("Lamp Hours")]
        public JoinDataComplete LampHours = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 53,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog,
                Label = "Lamp Hours"
            });

        [JoinName("ErrorMessage")]
        public JoinDataComplete ErrorMessage = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata { Description = "Error Message Feedback", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        public EpsonProjectorJoinMap(uint joinStart)
            : base(joinStart, typeof(EpsonProjectorJoinMap))
        {

        }
    }

    public class EpsonProjectorFactory : EssentialsDeviceFactory<EpsonProjector>
    {
        public EpsonProjectorFactory()
        {
            TypeNames = new List<string>() { "epsonProjector" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Epson Projector Device");

            var config = dc.Properties.ToObject<EpsonProjectorPropertiesConfig>();

            var comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new EpsonProjector(dc.Key, dc.Name, config, comm);
            else
                return null;
        }
    }

    public class EpsonProjectorPropertiesConfig
    {
        [JsonProperty("volumeUpperLimit")]
        public int? volumeUpperLimit { get; set; }

        [JsonProperty("volumeLowerLimit")]
        public int? volumeLowerLimit { get; set; }

        [JsonProperty("volumeSteps")]
        public int? volumeSteps { get; set; }

        [JsonProperty("defaultVolume")]
        public int? defaultVolume { get; set; }

        public EpsonProjectorPropertiesConfig()
        {
        }
    }
}