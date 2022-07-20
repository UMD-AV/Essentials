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
using Feedback = PepperDash.Essentials.Core.Feedback;

using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Devices.Displays
{
	/// <summary>
	/// 
	/// </summary>
    public class EpsonProjector : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced
	{
		public IBasicCommunication Communication { get; private set; }				
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        public IntFeedback LampHoursFeedback { get; private set; }
        public BoolFeedback VideoMuteIsOnFeedback { get; private set; }
        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }
        public BoolFeedback Input4Feedback { get; private set; }

        byte[] _tcpHandshake = new byte[] { 0x45, 0x53, 0x43, 0x2F, 0x56, 0x50, 0x2E, 0x6E, 0x65, 0x74, 0x10, 0x03, 0x00, 0x00, 0x00, 0x00 };
        byte[] _tcpHeader = new byte[] { 0x45, 0x53, 0x43, 0x2F, 0x56, 0x50, 0x2E, 0x6E, 0x65, 0x74, 0x10, 0x03, 0x00, 0x00 };
        bool _readyForCommands;
        bool _tcpComm;
        ushort _pollTracker;
		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
        bool _VideoMuteIsOn;
        int _LampHours;
        int _CurrentInputIndex;
        ushort _RequestedPowerState; // 0:none 1:on 2:off
        ushort _RequestedInputState; // 0:none 1-4:inputs 1-4 
        ushort _RequestedVideoMuteState; // 0:none/off 1:on

        readonly EpsonQueue _cmdQueue;
        readonly EpsonQueue _priorityQueue;
        CommunicationGather _PortGather;
        RoutingInputPort _CurrentInputPort;
        CMutex _CommandMutex;
        CMutex _PowerMutex;

		protected override Func<bool> PowerIsOnFeedbackFunc { get { return () => _PowerIsOn; } }
        protected override Func<bool> IsWarmingUpFeedbackFunc { get { return () => _IsWarmingUp; } }
		protected override Func<bool> IsCoolingDownFeedbackFunc { get { return () => _IsCoolingDown; } }
        protected override Func<string> CurrentInputFeedbackFunc { get { return () => _CurrentInputPort.Key; } }
 

		/// <summary>
		/// Constructor for IBasicCommunication
		/// </summary>
		public EpsonProjector(string key, string name, IBasicCommunication comm)
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
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            LampHoursFeedback = new IntFeedback(() => { return _LampHours; });
            VideoMuteIsOnFeedback = new BoolFeedback(() => { return _VideoMuteIsOn; });
            Input1Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 1; });
            Input2Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 2; });
            Input3Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 3; });
            Input4Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 4; });

            _pollTracker = 0;
            _LampHours = 0;
            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            _RequestedVideoMuteState = 0;
            WarmupTime = 60000;
            CooldownTime = 30000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

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
	    }

        void tcpComm_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (!e.Client.IsConnected)
            {
                _readyForCommands = false;
                _cmdQueue.ClearQueue();
                _priorityQueue.ClearQueue();
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
                        }
                        else if (data[1] == "03")
                        {
                            if (!_IsCoolingDown)
                            {
                                CrestronInvoke.BeginInvoke((o) => CooldownStart());
                            }
                        }
                        else if (data[1] == "00" || data[1] == "04" || data[1] == "09")
                        {
                            //Update power on feedback
                            if (_PowerIsOn == true)
                            {
                                _PowerIsOn = false;
                                PowerIsOnFeedback.FireUpdate();
                            }

                            //Clear power check
                            _PowerMutex.WaitForMutex();
                            if (_RequestedPowerState == 2)
                            {
                                _RequestedPowerState = 0;
                            }
                            _PowerMutex.ReleaseMutex();

                            //Finish cooling down process
                            if (_IsCoolingDown)
                            {
                                CrestronInvoke.BeginInvoke((o) =>CooldownDone());
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

                            if (newInput != null && newInput != _CurrentInputPort)
                            {
                                _CurrentInputPort = newInput;
                                CurrentInputFeedback.FireUpdate();
                                OnSwitchChange(new RoutingNumericEventArgs(null, _CurrentInputPort, eRoutingSignalType.AudioVideo));
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
                            _VideoMuteIsOn = true;

                        }
                        else if (data[1] == "OFF")
                        {
                            _VideoMuteIsOn = false;
                        }
                        VideoMuteIsOnFeedback.FireUpdate();
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
                    _RequestedVideoMuteState = 0;
                    VideoMuteOnGo();
                }
                if (_RequestedInputState != 0)
                {
                    if (_RequestedInputState != 0)
                    {
                        InputSelectGo(_RequestedInputState);
                    }
                }
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
                if (priority)
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
                while (_cmdQueue.Count > 0 || _priorityQueue.Count > 0)
                {
                    try
                    {
                        KeyValuePair<eCommandType, string> kvp;
                        if (_priorityQueue.Count > 0)
                        {
                            kvp = _priorityQueue.Dequeue();
                        }
                        else
                        {
                            kvp = _cmdQueue.Dequeue();
                        }
                        if (kvp.Value != null)
                        {
                            Debug.Console(1, this, "Sending Text: {0}", kvp.Value);
                            Communication.SendText(kvp.Value + "\x0D");
                            Thread.Sleep(500);
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
                }
                if (_pollTracker >= 7)
                {
                    LampHoursGet();
                    _pollTracker = 0;
                }
                _pollTracker++;
            }
            else if(Communication.IsConnected)
            {
                Debug.Console(1, this, "Sending tcp handshake");
                Communication.SendBytes(_tcpHandshake);
                CrestronEnvironment.Sleep(500);
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
                    CrestronEnvironment.Sleep(2000);
                }
            }
        }

        private void WarmupDone()
        {
            WarmupTimer.Stop();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

            VideoMuteGet();
            InputGet();

            if (_RequestedVideoMuteState == 1)
            {
                _RequestedVideoMuteState = 0;
                VideoMuteOnGo();
            }
            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }

            ProcessPower();

            //fail safe for no feedback
            if (!CommunicationMonitor.IsOnline)
            {
                _PowerMutex.WaitForMutex();
                _RequestedPowerState = 0;
                _PowerMutex.ReleaseMutex();
            }
        }

        private void CooldownCallback(object o)
        {
            Debug.Console(0, this, "Cooldown timed out");
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
                    CrestronEnvironment.Sleep(2000);
                }
            }
        }

        private void CooldownDone()
        {
            CooldownTimer.Stop();
            _IsWarmingUp = false;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            _VideoMuteIsOn = false;
            VideoMuteIsOnFeedback.FireUpdate();

            ProcessPower();

            //fail safe for no feedback
            if (!CommunicationMonitor.IsOnline)
            {
                _PowerMutex.WaitForMutex();
                _RequestedPowerState = 0;
                _PowerMutex.ReleaseMutex();
            }
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
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedVideoMuteState = 0;
                VideoMuteOnGo();
            }
            else if(_RequestedPowerState == 1)
            {
                _RequestedVideoMuteState = 1;
            }
        }

        private void VideoMuteOnGo()
        {
            SendCommand(eCommandType.VideoMute, "MUTE ON", false);
            VideoMuteGet();
            CrestronInvoke.BeginInvoke((o) => {
                CrestronEnvironment.Sleep(1000);
                VideoMuteGet();
            });
        }

        public void VideoMuteOff()
        {
            _RequestedVideoMuteState = 0;
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
                CrestronEnvironment.Sleep(1000);
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
            _RequestedInputState = 0;
        }

		public void InputHdmi1()
		{
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputHdmi1Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 1;
            }
		}

        public void InputHdmi2()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputHdmi2Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 2;
            }
        }

        public void InputNetwork()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputNetworkGo();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 3;
            }
        }

        public void InputVga()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputVgaGo();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 4;
            }
        }

        private void InputHdmi1Go()
        {
            if (_CurrentInputIndex != 1)
            {
                SendCommand(eCommandType.Input, "SOURCE 30", false);
                InputGet();
                VideoMuteGet();
            }
        }

        public void InputHdmi2Go()
        {
            if (_CurrentInputIndex != 2)
            {
                SendCommand(eCommandType.Input, "SOURCE A0", false);
                InputGet();
                VideoMuteGet();
            }
        }

		public void InputNetworkGo()
		{
            if (_CurrentInputIndex != 3)
            {
                SendCommand(eCommandType.Input, "SOURCE 80", false);
                InputGet();
                VideoMuteGet();
            }
        }

        public void InputVgaGo()
        {
            if (_CurrentInputIndex != 4)
            {
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
            LampPoll
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

            var comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new EpsonProjector(dc.Key, dc.Name, comm);
            else
                return null;
        }
    }
}