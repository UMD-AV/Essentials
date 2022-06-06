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
        string _escVpNetHeader = "ESC/VP.net\u0010\u0003\u0000\u0000";
        bool _tcpControl;
        bool _readyForCommands;
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

        readonly CrestronQueue<string> _cmdQueue;
        readonly CrestronQueue<string> _priorityQueue;
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
            _PortGather = new CommunicationGather(Communication, '\x0D');
            _PortGather.IncludeDelimiter = false;
            _PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(Communication_TextReceived);

            var tcpComm = comm as GenericTcpIpClient;
            if (tcpComm != null)
            {
                _tcpControl = true;
                _readyForCommands = false;
                tcpComm.AutoReconnect = true;
                tcpComm.AutoReconnectIntervalMs = 10000;
                tcpComm.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(tcpComm_ConnectionChange);
            }
            else
            {
                _tcpControl = false;
                _readyForCommands = true;
            }

            _cmdQueue = new CrestronQueue<string>();
            _priorityQueue = new CrestronQueue<string>();
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
            WarmupTime = 30000;
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

            StatusGet();
		}

        void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
		public override bool CustomActivate()
		{
			Communication.Connect();
			CommunicationMonitor.StatusChange += (o, a) => Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
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

	    }

        void tcpComm_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            Debug.Console(1, this, "Tcp status: {0}", e.Client.ClientStatus);
            if (e.Client.IsConnected)
            {
                while (e.Client.IsConnected && !_readyForCommands)
                {
                    Debug.Console(1, this, "Sending tcp handshake: {0}", _tcpHandshake.ToString());
                    e.Client.SendBytes(_tcpHandshake);
                    CrestronEnvironment.Sleep(5000);
                }
            }
            else
            {
                _readyForCommands = false;
                _cmdQueue.Clear();
                _priorityQueue.Clear();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        void Communication_TextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            try
            {
                string feedback = e.Text.Replace(":", "").Trim();
                Debug.Console(1, this, "Feedback: {0}", feedback);

                //Check for tcpHeader reponse
                if (_tcpControl && !_readyForCommands && feedback.StartsWith(_escVpNetHeader))
                {
                    if (feedback.Length > _escVpNetHeader.Length + 1)
                    {
                        char statusCode = feedback[_escVpNetHeader.Length];
                        if (statusCode == '\x20')
                        {
                            Debug.Console(0, this, "EpsonProjector connected");
                            _readyForCommands = true;
                            Resync();
                        }
                        else
                        {
                            Debug.Console(0, this, "EpsonProjector password required");
                            _readyForCommands = false;
                        }
                    }
                    return;
                }

                string[] data = feedback.Split('=');

                if(data.Length > 1)
                {
                    if (data[0] == "PWR")
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
                                WarmupDone();
                            }
                        }
                        else if (data[1] == "02")
                        {
                            if (!_IsWarmingUp)
                            {
                                WarmupStart();
                            }
                        }
                        else if (data[1] == "03")
                        {
                            if (!_IsCoolingDown)
                            {
                                CooldownStart();
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
                                CooldownDone();
                            }
                        }
                    }
                    else if (data[0] == "SOURCE")
                    {
                        try
                        {
                            int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(data[1]));
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
                            Debug.Console(1, this, "Invalid input feedback: {0}", data[1]);
                        }
                    }
                    else if (data[0] == "LAMP")
                    {
                        int newHours = int.Parse(data[1]);

                        if(_LampHours != newHours)
                        {
                            _LampHours = newHours;
                            LampHoursFeedback.FireUpdate();
                        }
                    }
                    else if (data[0] == "MUTE")
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
        public void SendCommand(string cmd, bool priority)
        {
            if (_readyForCommands)
            {
                Debug.Console(1, this, "Enqueuing command: {0}", cmd);
                if (priority)
                {
                    _priorityQueue.Enqueue(cmd);
                }
                else
                {
                    _cmdQueue.Enqueue(cmd);
                }
                ProcessQueue();
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
                    Debug.Console(1, this, "Processing Queue");
                    try
                    {
                        string cmd;
                        if (_priorityQueue.Count > 0)
                        {
                            cmd = _priorityQueue.Dequeue();
                        }
                        else
                        {
                            cmd = _cmdQueue.Dequeue();
                        }
                        if (cmd != null)
                        {
                            Debug.Console(1, this, "Sending Text: {0}", cmd);
                            Communication.SendText(cmd + "\x0D");
                            Thread.Sleep(200);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Caught an exception in SendCommand {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
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

                if (_PowerIsOn && !_IsWarmingUp && (_pollTracker % 4 == 0))
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
        }

        private void WarmupCallback(object o)
        {
            WarmupDone();
        }

        private void WarmupStart()
        {
            CooldownTimer.Stop();
            _PowerIsOn = true;
            PowerIsOnFeedback.FireUpdate();
            _IsWarmingUp = true;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            WarmupTimer.Reset(WarmupTime);
            while (_IsWarmingUp)
            {
                SendCommand("PWR?", true);
                Thread.Sleep(1000);
            }
        }

        private void WarmupDone()
        {
            WarmupTimer.Stop();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

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
            CooldownDone();
        }

        private void CooldownStart()
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
                SendCommand("PWR?", true);
                Thread.Sleep(1000);
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
            ProcessPower();
            _RequestedInputState = 0;
            _RequestedVideoMuteState = 0;
            _RequestedInputState = 0;
		}

        private void PowerOnGo()
        {
            SendCommand("PWR ON", true);
            CrestronInvoke.BeginInvoke((o) => WarmupStart());
        }

        private void PowerOffGo()
        {
            SendCommand("PWR OFF", true);
            CrestronInvoke.BeginInvoke((o) => CooldownStart());
        }

        private void ProcessPower()
        {
            bool test = _PowerMutex.WaitForMutex(100);
            if (test)
            {
                try
                {
                    if (!_IsWarmingUp && !_IsCoolingDown)
                    {
                        if (_RequestedPowerState == 1 && _PowerIsOn == false)
                        {
                            PowerOnGo();
                        }
                        else if (_RequestedPowerState == 2 && _PowerIsOn == true)
                        {
                            PowerOffGo();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Caught an exception in ProcessPower {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                }
                finally
                {
                    _PowerMutex.ReleaseMutex();
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
            SendCommand("PWR?", false);
        }

        public void VideoMuteOn()
        {
            if (_PowerIsOn)
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
            SendCommand("MUTE ON", false);
            CrestronEnvironment.Sleep(500);
            VideoMuteGet();
        }

        public void VideoMuteOff()
        {
            _RequestedVideoMuteState = 0;
            if (_PowerIsOn)
            {
                VideoMuteOffGo();
            }
        }

        private void VideoMuteOffGo()
        {
            SendCommand("MUTE OFF", false);
            CrestronEnvironment.Sleep(500);
            VideoMuteGet();
        }

        public void VideoMuteGet()
        {
            SendCommand("MUTE?", false);
        }

        public void LampHoursGet()
        {
            SendCommand("LAMP?", false);
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
            if (_PowerIsOn)
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
            if (_PowerIsOn)
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
            if (_PowerIsOn)
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
            if (_PowerIsOn)
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
            SendCommand("SOURCE 30", false);
            CrestronEnvironment.Sleep(500);
            InputGet();
        }

        public void InputHdmi2Go()
        {
            SendCommand("SOURCE A0", false);
            CrestronEnvironment.Sleep(500);
            InputGet();
        }

		public void InputNetworkGo()
		{
            SendCommand("SOURCE 80", false);
            CrestronEnvironment.Sleep(500);
            InputGet();
        }

        public void InputVgaGo()
        {
            SendCommand("SOURCE 11", false);
            CrestronEnvironment.Sleep(500);
            InputGet();
        }

        public void InputGet()
        {
            SendCommand("SOURCE?", false);
        }

        /// <summary>
        /// Executes a switch, turning on display if necessary.
        /// </summary>
        /// <param name="selector"></param>
		public override void ExecuteSwitch(object selector)
		{
            if (_PowerIsOn)
                (selector as Action)();
            else
            {
                // One-time event handler to wait for power on before executing switch
                EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
                handler = (o, a) =>
                {
                    if (!_IsWarmingUp) // Done warming
                    {
                        IsWarmingUpFeedback.OutputChange -= handler;
                        (selector as Action)();
                    }
                };
                IsWarmingUpFeedback.OutputChange += handler; // attach and wait for on FB
                PowerOn();
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