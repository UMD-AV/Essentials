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
using Newtonsoft.Json;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Displays
{
	/// <summary>
	/// 
	/// </summary>
    public class NecDisplay : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced
	{
		public IBasicCommunication Communication { get; private set; }				
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #region Command constants
        public const string PowerGetCmd = "01D6";
        public const string InputGetCmd = "0060";

        public const string Hdmi1Cmd =    "00600011";
        public const string Hdmi2Cmd =    "00600012";
        public const string Hdmi3Cmd =    "00600082";
        public const string Hdmi4Cmd =    "00600083";
        public const string Dp1Cmd =      "0060000F";
        public const string Dp2Cmd =      "00600010";
        public const string Dvi1Cmd =     "00600003";
        public const string Video1Cmd =   "00600005";
        public const string VgaCmd =      "00600001";
        public const string RgbCmd =      "00600002";

        public const string PowerOnCmd =  "C203D60001";
        public const string PowerOffCmd = "C203D60004";
        #endregion

        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }
        public BoolFeedback Input4Feedback { get; private set; }

        byte _displayID = Convert.ToByte('A');
        bool _readyForCommands;
        bool _tcpComm;
		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
        int _CurrentInputIndex;
        ushort _RequestedPowerState; // 0:none 1:on 2:off
        ushort _RequestedInputState; // 0:none 1-4:inputs 1-4 

        string videoMuteKey;
        int videoMuteInput;
        private DM.DmRmcControllerBase _scaler;
        readonly NecQueue _cmdQueue;
        readonly NecQueue _priorityQueue;
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
		public NecDisplay(string key, string name, IBasicCommunication comm, NecDisplayPropertiesConfig config)
			: base(key, name)
		{
			Communication = comm;
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

            _cmdQueue = new NecQueue();
            _priorityQueue = new NecQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            Input1Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 1; });
            Input2Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 2; });
            Input3Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 3; });
            Input4Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 4; });

            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            WarmupTime = 15000;
            CooldownTime = 15000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

            if (config.VideoMuteKey != null)
            {
                videoMuteKey = config.VideoMuteKey;
            }
            videoMuteInput = config.VideoMuteInput;

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            AddRoutingInputPort(new RoutingInputPort("HDMI 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "11");

            AddRoutingInputPort(new RoutingInputPort("HDMI 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "12");

            AddRoutingInputPort(new RoutingInputPort("DP 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DisplayPort, new Action(InputDp1), this), "0F");

            AddRoutingInputPort(new RoutingInputPort("DP 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DisplayPort, new Action(InputDp2), this), "10");
		}

        void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        public override bool CustomActivate()
        {
            if (videoMuteKey != null)
            {
                var dev = DeviceManager.GetDeviceForKey(videoMuteKey);
                if (dev is DM.DmRmcControllerBase)
                {
                    Debug.Console(0, this, "Using scaler {0} for video mute", videoMuteKey);
                    _scaler = dev as DM.DmRmcControllerBase;
                }
            }

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
            var joinMap = new NecDisplayJoinMap(joinStart);

            trilist.BooleanInput[joinMap.LampHoursSupported.JoinNumber].BoolValue = false;

            //Video Mute
            if (_scaler != null)
            {
                _scaler.HdmiOutputBlankedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VideoMuteOn.JoinNumber]);
                trilist.SetSigTrueAction(joinMap.VideoMuteOn.JoinNumber, _scaler.BlankOutput);
                trilist.SetSigTrueAction(joinMap.VideoMuteOff.JoinNumber, _scaler.UnblankOutput);

                //If config has video mute input defined, only support scaler video mute while on that display input
                if (videoMuteInput > 0)
                {

                    CurrentInputFeedback.OutputChange += (o, args) =>
                    {
                        if (videoMuteInput == _CurrentInputIndex)
                        {
                            trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = true;
                        }
                        else
                        {
                            trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = false;
                        }
                    };
                }
                else
                {
                    trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = true;
                }
            }

            IsWarmingUpFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Warming.JoinNumber]);
            IsCoolingDownFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Cooling.JoinNumber]);
            Input1Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 0]);
            Input2Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 1]);
            Input3Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 2]);
            Input4Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 3]);
	    }

        private enum eNecMessageType : byte
        {
            Command = 0x41,
            CommandReply = 0x42,
            Get = 0x43,
            GetReply = 0x44,
            Set = 0x45,
            SetReply = 0x46            
        }

        public byte[] PrepareCommand(string command)
        {
            int commandLength = command.Length + 2; // Add STX and ETX
            int fullLength = commandLength + 9; //Full command length is header (7) + command + checksum (1) + CR (1)
            var commandB = new byte[fullLength]; 
            
            //Build the header for the first 7 bytes
            commandB[0] = 0x01; //SOH
            commandB[1] = 0x30; //Reserved
            commandB[2] = _displayID; //Display ID
            commandB[3] = 0x30; //Reserved

            //Header byte 4
            if (command == InputGetCmd)
                commandB[4] = (byte)eNecMessageType.Get;
            else if (command == PowerOnCmd || command == PowerOffCmd || command == PowerGetCmd)
                commandB[4] = (byte)eNecMessageType.Command;
            else
                commandB[4] = (byte)eNecMessageType.Set;

            //Header byte 5 & 6 are ASCII representation of hex length of command
            byte lengthB = Convert.ToByte(commandLength);

            // Byte 5 - This is to take the actual number and map it to ther ascii value
            commandB[5] = Convert.ToByte((lengthB & 0xF0));
            if (commandB[5] <= 0x09)
            {
                commandB[5] += 0x30;
            }
            else
            {
                commandB[5] += 0x37;
            }

            // Byte 6 - This is to take the actual number and map it to ther ascii value
            commandB[6] = Convert.ToByte((lengthB & 0x0F));
            if (commandB[6] <= 0x09)
            {
                commandB[6] += 0x30;
            }
            else
            {
                commandB[6] += 0x37;
            }

            //Header complete, now build command hex
            commandB[7] = 0x02; //Add Start TX
            for (int i = 0; i < commandLength-2; i++)
            {
                commandB[i + 8] = Convert.ToByte(command[i]);
            }
            commandB[commandLength + 6] = 0x03; //Add End TX
            
            //Now generate checksum, starting at byte 1 (skip SOH, byte 0)
            byte checksum = commandB[1];
            for (var i = 2; i < fullLength - 2; i++)
            {
                checksum = Convert.ToByte(checksum ^ commandB[i]);
            }
            commandB[fullLength - 2] = checksum;

            //Now add CR
            commandB[fullLength - 1] = 0x0D;

            return commandB;
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
            else
            {
                _readyForCommands = true;
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
                byte[] feedbackBytes = Encoding.GetEncoding(28591).GetBytes(e.Text);
                Debug.Console(1, this, "Feedback: {0}", ComTextHelper.GetEscapedText(feedbackBytes));

                int startPos = -1;
                //Try to trim any beginning garbage, but only check to (length - 10) for start position since min length is at least 10 bytes
                for (int i = 0; (i + 10) < feedbackBytes.Length; i++)
                {
                    if(feedbackBytes[i] == 0x01 
                    && feedbackBytes[i + 1] == '0'
                    && feedbackBytes[i + 2] == '0'
                    && feedbackBytes[i + 3] == _displayID
                    && feedbackBytes[i + 7] == 0x02)
                    {
                        startPos = i;
                        break;
                    }
                }

                //Check for header
                if (startPos == -1)
                {
                    Debug.Console(1, this, "Feedback does not have a valid header");
                    return;
                }
                if (!Enum.IsDefined(typeof(eNecMessageType), feedbackBytes[startPos + 4]))
                {
                    Debug.Console(1, this, "Feedback is not a valid message type: {0:X2}", feedbackBytes[startPos + 4]);
                    return;
                }

                eNecMessageType messageType = (eNecMessageType)feedbackBytes[startPos + 4];
                //Found a valid get reply, now trim the header, STX (start of message byte)
                string parsedFb = e.Text.Substring(startPos + 8);

                if(messageType == eNecMessageType.CommandReply)
                {
                    if (parsedFb.Length >= 16 && parsedFb.StartsWith("0200D6"))
                    {
                        Debug.Console(1, this, "Found valid power status feedback: {0}", parsedFb[15]);
                        ProcessPowerFb(parsedFb.Substring(12, 4));
                    }
                    else if (parsedFb.Length >= 12 && parsedFb.StartsWith("00C203D6"))
                    {
                        Debug.Console(1, this, "Found valid power setting confirmation: {0}", parsedFb[11]);
                    }
                    else if(parsedFb.StartsWith("0201D6") || parsedFb.StartsWith("01C203D6"))
                    {
                        Debug.Console(1, this, "Command reply result code is an error: {0}", ComTextHelper.GetEscapedText(parsedFb));
                        return;
                    }
                }
                else if (messageType == eNecMessageType.GetReply)
                {
                    if (parsedFb.StartsWith("01"))
                    {
                        Debug.Console(1, this, "Get reply result code is an error: {0}", ComTextHelper.GetEscapedText(parsedFb));
                        return;
                    }
                    if (parsedFb.Length >= 16 && parsedFb.StartsWith("00006000"))
                    {
                        try
                        {
                            string inputFb = parsedFb.Substring(14, 2);
                            Debug.Console(1, this, "Found valid input feedback reply: {0}", inputFb);
                            int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(inputFb));
                            var newInput = InputPorts[index];
                            if (_CurrentInputIndex != (index + 1))
                            {
                                _CurrentInputIndex = index + 1; //Offset from 0 based index
                                Input1Feedback.FireUpdate();
                                Input2Feedback.FireUpdate();
                                Input3Feedback.FireUpdate();
                                Input4Feedback.FireUpdate();
                            }

                            if (newInput != null && newInput != _CurrentInputPort)
                            {
                                _CurrentInputPort = newInput;
                                CurrentInputFeedback.FireUpdate();
                                OnSwitchChange(new RoutingNumericEventArgs(null, _CurrentInputPort, eRoutingSignalType.AudioVideo));
                            }
                        }
                        catch
                        {
                            Debug.Console(1, this, "Invalid input feedback: {0}", feedbackBytes);
                        }
                    }
                }
                else if(messageType == eNecMessageType.SetReply)
                {
                    Debug.Console(1, this, "Found set reply: {0}", ComTextHelper.GetEscapedText(parsedFb));
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }
        }

        private void ProcessPowerFb(string powerFb)
        {
            if (powerFb == "0001")
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
            else if (powerFb == "0002" || powerFb == "0003" || powerFb == "0004")
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
                    CrestronInvoke.BeginInvoke((o) => CooldownDone());
                }
            }
        }

        private void Resync()
        {
            StatusGet();
            ProcessPower();
            if (!_IsCoolingDown && !_IsWarmingUp)
            {
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
                Debug.Console(1, this, "Nec display not connected, ignoring command");
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
                            byte[] command = PrepareCommand(kvp.Value);
                            Debug.Console(1, this, "Sending bytes: {0}", ComTextHelper.GetEscapedText(command));
                            Communication.SendBytes(PrepareCommand(kvp.Value));
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
                InputGet();
            }
        }

        private void WarmupCallback(object o)
        {
            Debug.Console(1, this, "Warmup complete");
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
            }
        }

        private void WarmupDone()
        {
            WarmupTimer.Stop();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

            InputGet();

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
            Debug.Console(1, this, "Cooldown complete");
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
            }
        }

        private void CooldownDone()
        {
            CooldownTimer.Stop();
            _IsWarmingUp = false;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

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
            _RequestedInputState = 0;
            ProcessPower();
		}

        private void PowerOnGo()
        {
            SendCommand(eCommandType.Power, PowerOnCmd, true);
            CrestronInvoke.BeginInvoke((o) => WarmupStart());
        }

        private void PowerOffGo()
        {
            if (_scaler != null)
            {
                _scaler.UnblankOutput();
            }

            SendCommand(eCommandType.Power, PowerOffCmd, true);
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
            SendCommand(eCommandType.PowerPoll, PowerGetCmd, false);
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
                    InputDp1();
                    break;
                case 4:
                    InputDp2();
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
                    InputDp1Go();
                    break;
                case 4:
                    InputDp2Go();
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

        public void InputDp1()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputDp1Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 3;
            }
        }

        public void InputDp2()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputDp2Go();
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
                SendCommand(eCommandType.Input, Hdmi1Cmd, false);
                InputGet();
            }
        }

        public void InputHdmi2Go()
        {
            if (_CurrentInputIndex != 2)
            {
                SendCommand(eCommandType.Input, Hdmi2Cmd, false);
                InputGet();
            }
        }

		public void InputDp1Go()
		{
            if (_CurrentInputIndex != 3)
            {
                SendCommand(eCommandType.Input, Dp1Cmd, false);
                InputGet();
            }
        }

        public void InputDp2Go()
        {
            if (_CurrentInputIndex != 4)
            {
                SendCommand(eCommandType.Input, Dp2Cmd, false);
                InputGet();
            }
        }

        public void InputGet()
        {
            SendCommand(eCommandType.InputPoll, InputGetCmd, false);
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
            PowerPoll,
            InputPoll
        }

        private class NecQueue
        {
            public List<KeyValuePair<eCommandType, string>> Q = new List<KeyValuePair<eCommandType, string>>();
            public ushort Count { get { return (ushort)Q.Count; } }
            private CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing Nec Display commands
            /// </summary>
            public NecQueue()
            {
            }

            public void AddOrUpdateCommand(KeyValuePair<eCommandType, string> command)
            {
                mutex.WaitForMutex();
                try
                {
                    int i = Q.FindIndex(x => x.Key.Equals(command.Key));
                    if (i != -1 && (command.Key == eCommandType.Input))
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
                    Debug.Console(1, "Exception in Nec command queue add/update: {0}", ex);
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
                    Debug.Console(1, "Exception in Nec command queue clear: {0}", ex);
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
                    Debug.Console(1, "Exception in Nec command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
                return kvp;
            }
        }
	}

    public class NecDisplayJoinMap : DisplayControllerJoinMap
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

        public NecDisplayJoinMap(uint joinStart)
            : base(joinStart, typeof(NecDisplayJoinMap))
        {

        }
    }

    public class NecDisplayPropertiesConfig
    {
        [JsonProperty("videoMuteKey")]
        public string VideoMuteKey { get; set; }

        [JsonProperty("videoMuteInput")]
        public int VideoMuteInput { get; set; }
    }

    public class NecDisplayFactory : EssentialsDeviceFactory<NecDisplay>
    {
        public NecDisplayFactory()
        {
            TypeNames = new List<string>() { "nec" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Nec Display Device");

            var config = dc.Properties.ToObject<NecDisplayPropertiesConfig>();
            var comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new NecDisplay(dc.Key, dc.Name, comm, config);
            else
                return null;
        }
    }
}