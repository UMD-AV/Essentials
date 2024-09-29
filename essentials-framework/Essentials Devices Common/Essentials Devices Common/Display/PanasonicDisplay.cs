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
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.Devices.Displays
{
	/// <summary>
	/// 
	/// </summary>
    public class PanasonicDisplay : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced
	{
		public IBasicCommunication Communication { get; private set; }				
        public StatusMonitorBase CommunicationMonitor { get; private set; }

		#region Command constants
		public const string InputGetCmd = "\x02QMI\x03";
		public const string Hdmi1Cmd = "\x02IMS:HM1\x03";
		public const string Hdmi2Cmd = "\x02IMS:HM2\x03";
        public const string Pc1Cmd = "\x02IMS:PC1\x03";

        public const string PowerGetCmd = "\x02QPW\x03";
		public const string PowerOnCmd = "\x02PON\x03";
		public const string PowerOffCmd = "\x02POF\x03";

		public const string MuteOffCmd = "\x02AMT:0\x03";
		public const string MuteOnCmd = "\x02AMT:1\x03";
		public const string MuteGetCmd = "\x02QAM\x03";

		public const string VolumeGetCmd = "\x02QAV\x03";
		public const string VolumeLevelPartialCmd = "\x02AVL:";
		public const string VolumeUpCmd = "\x02AUU\x03";
		public const string VolumeDownCmd = "\x02AUD\x03";
		#endregion

        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }

        bool _readyForCommands;
        bool _tcpComm;
		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
        int _CurrentInputIndex;
        ushort _RequestedPowerState; // 0:none 1:on 2:off
        ushort _RequestedInputState; // 0:none 1-3:inputs 1-3 

        private DM.DmRmcControllerBase _scaler;
        readonly PanasonicQueue _cmdQueue;
        readonly PanasonicQueue _priorityQueue;
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
		public PanasonicDisplay(string key, string name, IBasicCommunication comm, PanasonicDisplayPropertiesConfig config)
			: base(key, name)
		{
			Communication = comm;
            _PortGather = new CommunicationGather(Communication, '\x03');
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

            _cmdQueue = new PanasonicQueue();
            _priorityQueue = new PanasonicQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            Input1Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 1; });
            Input2Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 2; });
            Input3Feedback = new BoolFeedback(() => { return _CurrentInputIndex == 3; });

            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            WarmupTime = 15000;
            CooldownTime = 15000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            AddRoutingInputPort(new RoutingInputPort("HDMI 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "HM1");

            AddRoutingInputPort(new RoutingInputPort("HDMI 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "HM2");

            AddRoutingInputPort(new RoutingInputPort("PC 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Vga, new Action(InputPc1), this), "PC1");

            if(config.VideoMuteKey != null)
            {
                var dev = DeviceManager.GetDeviceForKey(config.VideoMuteKey);
                if (dev is DM.DmRmcControllerBase)
                {
                    _scaler = dev as DM.DmRmcControllerBase;
                }
            }
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
            var joinMap = new PanasonicDisplayJoinMap(joinStart);

            trilist.BooleanInput[joinMap.LampHoursSupported.JoinNumber].BoolValue = false;
            trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = false;

            //Video Mute
            if(_scaler != null)
            {
                _scaler.HdmiOutputBlankedFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VideoMuteOn.JoinNumber]);
                trilist.SetSigTrueAction(joinMap.VideoMuteOn.JoinNumber, _scaler.BlankOutput);
                trilist.SetSigTrueAction(joinMap.VideoMuteOff.JoinNumber, _scaler.UnblankOutput);
            }

            IsWarmingUpFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Warming.JoinNumber]);
            IsCoolingDownFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Cooling.JoinNumber]);
            Input1Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 0]);
            Input2Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 1]);
            Input3Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 2]);
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
                if (e.Text.Contains("\x02"))
                {
                    int start = e.Text.IndexOf("\x02");
                    string fb = e.Text.Remove(0, start + 1);    //Remove STX and any possible data before the STX
                    Debug.Console(1, this, "Received feedback: {0}", fb);
                    if (fb.StartsWith("QPW"))
                    {
                        ProcessPowerFb(fb);
                    }
                    else if (fb.StartsWith("QMI:"))
                    {
                        string inputFb = fb.Remove(0, 4);
                        if (inputFb.Length > 0)
                        {
                            if (inputFb.Length > 3)
                                inputFb = inputFb.Substring(0, 3);
                            ProcessInputFb(inputFb);
                        }
                    }
                }
                else
                {
                    Debug.Console(1, this, "Garbage feedback, discarding: {0}", e.Text);
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }
        }

        private void ProcessPowerFb(string powerFb)
        {
            if (powerFb == "QPW:1")
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
            else if(powerFb == "QPW:0")
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

        private void ProcessInputFb(string inputFb)
        {
            try
            {
                Debug.Console(1, this, "Found valid input feedback reply: {0}", inputFb);
                int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(inputFb));
                var newInput = InputPorts[index];
                if (_CurrentInputIndex != (index + 1))
                {
                    _CurrentInputIndex = index + 1; //Offset from 0 based index
                    Input1Feedback.FireUpdate();
                    Input2Feedback.FireUpdate();
                    Input3Feedback.FireUpdate();
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
                Debug.Console(1, this, "Invalid input feedback: {0}", inputFb);
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
                Debug.Console(1, this, "Panasonic display not connected, ignoring command");
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
                            Debug.Console(1, this, "Sending command: {0}", ComTextHelper.GetEscapedText(kvp.Value));
                            Communication.SendText(kvp.Value);
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
                    InputPc1();
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
                    InputPc1Go();
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

        public void InputPc1()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputPc1Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 3;
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

		public void InputPc1Go()
		{
            if (_CurrentInputIndex != 3)
            {
                SendCommand(eCommandType.Input, Pc1Cmd, false);
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

        private class PanasonicQueue
        {
            public List<KeyValuePair<eCommandType, string>> Q = new List<KeyValuePair<eCommandType, string>>();
            public ushort Count { get { return (ushort)Q.Count; } }
            private CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing Panasonic Display commands
            /// </summary>
            public PanasonicQueue()
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
                    Debug.Console(1, "Exception in Panasonic command queue add/update: {0}", ex);
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
                    Debug.Console(1, "Exception in Panasonic command queue clear: {0}", ex);
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
                    Debug.Console(1, "Exception in Panasonic command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
                return kvp;
            }
        }
	}

    public class PanasonicDisplayJoinMap : DisplayControllerJoinMap
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

        public PanasonicDisplayJoinMap(uint joinStart)
            : base(joinStart, typeof(PanasonicDisplayJoinMap))
        {

        }
    }

    public class PanasonicDisplayPropertiesConfig
    {
        [JsonProperty("videoMuteKey")]
        public string VideoMuteKey { get; set; }
    }

    public class PanasonicDisplayFactory : EssentialsDeviceFactory<PanasonicDisplay>
    {
        public PanasonicDisplayFactory()
        {
            TypeNames = new List<string>() { "panasonicdisplay" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Panasonic Display Device");

            var config = dc.Properties.ToObject<PanasonicDisplayPropertiesConfig>();
            var comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new PanasonicDisplay(dc.Key, dc.Name, comm, config);
            else
                return null;
        }
    }
}