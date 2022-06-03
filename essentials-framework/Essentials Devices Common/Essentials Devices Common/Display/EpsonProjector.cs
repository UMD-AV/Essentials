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

		bool _PowerIsOn;
		bool _IsWarmingUp;
		bool _IsCoolingDown;
        bool _VideoMuteIsOn;
        int _LampHours;
        int _LastPowerState; // 0=don't care 1=on 2=off

        readonly CrestronQueue<string> _cmdQueue;
        readonly CrestronQueue<string> _priorityQueue;
        CommunicationGather _PortGather;
        RoutingInputPort _CurrentInputPort;
        CMutex _CommandMutex;
        CMutex _PowerMutex;
        CTimer _WarmupTimer;
        CTimer _CooldownTimer;

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

            _cmdQueue = new CrestronQueue<string>();
            _priorityQueue = new CrestronQueue<string>();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            LampHoursFeedback = new IntFeedback(() => { return _LampHours; });
            VideoMuteIsOnFeedback = new BoolFeedback(() => { return _VideoMuteIsOn; });

            _LastPowerState = 0;
            WarmupTime = 30000;
            CooldownTime = 20000;
            _WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            _CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

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
                string[] data = feedback.Split('=');

                if(data.Length > 1)
                {
                    if (data[0] == "PWR")
                    {
                        if (data[1] == "01")
                        {
                            _PowerIsOn = true;
                            if (_IsWarmingUp)
                            {
                                WarmupDone();
                            }
                        }
                        else if (data[1] == "02")
                        {
                            _PowerIsOn = true;
                            _IsWarmingUp = true;
                            _IsCoolingDown = false;
                            IsWarmingUpFeedback.FireUpdate();
                            IsCoolingDownFeedback.FireUpdate();
                        }
                        else if (data[1] == "03")
                        {
                            _PowerIsOn = false;
                            _IsWarmingUp = false;
                            _IsCoolingDown = true;
                            IsWarmingUpFeedback.FireUpdate();
                            IsCoolingDownFeedback.FireUpdate();
                        }
                        else if (data[1] == "00" || data[1] == "04" || data[1] == "09")
                        {
                            _PowerIsOn = false;
                            if (_IsCoolingDown)
                            {
                                CooldownDone();
                            }
                        }
                        PowerIsOnFeedback.FireUpdate();

                    }
                    else if (data[0] == "SOURCE")
                    {
                        Debug.Console(1, this, "Feedback found SOURCE");
                        var newInput = InputPorts.FirstOrDefault(i => i.FeedbackMatchObject.Equals(data[1]));
                        if (newInput != null && newInput != _CurrentInputPort)
                        {
                            _CurrentInputPort = newInput;
                            CurrentInputFeedback.FireUpdate();
                            OnSwitchChange(new RoutingNumericEventArgs(null, _CurrentInputPort, eRoutingSignalType.AudioVideo));
                        }
                    }
                    else if (data[0] == "LAMP")
                    {
                        Debug.Console(1, this, "Feedback found LAMP");
                        int newHours = int.Parse(data[1]);

                        if(_LampHours != newHours)
                        {
                            _LampHours = newHours;
                            LampHoursFeedback.FireUpdate();
                        }
                    }
                    else if (data[0] == "MUTE")
                    {
                        Debug.Console(1, this, "Feedback found MUTE");
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

        /// <summary>
        /// 
        /// </summary>
        public void SendCommand(string cmd)
        {
            Debug.Console(1, this, "Enqueuing command: {0}", cmd);
            _cmdQueue.Enqueue(cmd);

            ProcessQueue();
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (test)
            {
                //Pace the commands sending out
                while (_cmdQueue.Count > 0)
                {
                    Debug.Console(1, this, "Processing Queue");
                    try
                    {
                        string cmd = _cmdQueue.Dequeue();
                        if (cmd != null)
                        {
                            Debug.Console(1, this, "Sending Text: {0}", cmd);
                            Communication.SendText(cmd + "\x0D");
                            Thread.Sleep(500);
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
            PowerGet();
            LampHoursGet();
            if (_PowerIsOn == true && _IsWarmingUp == false)
            {
                //Only poll these while projector is warmed up and on, otherwise it responds "ERR"
                VideoMuteGet();
                InputGet();
            }
        }

        private void WarmupCallback(object o)
        {
            WarmupDone();
        }

        private void CooldownCallback(object o)
        {
            CooldownDone();
        }

        private void WarmupDone()
        {
            _WarmupTimer.Stop();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            ProcessPower();
        }

        private void CooldownDone()
        {
            _CooldownTimer.Stop();
            _IsWarmingUp = false;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            ProcessPower();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PowerOn()
		{
            _LastPowerState = 1;
            ProcessPower();
		}

        /// <summary>
        /// 
        /// </summary>
		public override void PowerOff()
		{
            _LastPowerState = 2;
            ProcessPower();
		}

        private void PowerOnGo()
        {
            _priorityQueue.Enqueue("PWR ON");
            _PowerIsOn = true;
            _IsWarmingUp = true;
            _IsCoolingDown = false;
            PowerIsOnFeedback.FireUpdate();
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            _WarmupTimer.Reset(WarmupTime);
            while (_IsWarmingUp)
            {
                _priorityQueue.Enqueue("PWR?");
                Thread.Sleep(1000);
            }
        }

        private void PowerOffGo()
        {
            _priorityQueue.Enqueue("PWR OFF");
            _PowerIsOn = false;
            _IsCoolingDown = true;
            _IsWarmingUp = false;
            PowerIsOnFeedback.FireUpdate();
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();
            _CooldownTimer.Reset(CooldownTime);
            while (_IsCoolingDown)
            {
                _priorityQueue.Enqueue("PWR?");
                Thread.Sleep(1000);
            }
        }

        private void ProcessPower()
        {
            bool test = _PowerMutex.WaitForMutex(100);
            if (test)
            {
                if (!_IsWarmingUp && !_IsCoolingDown)
                {
                    if (_LastPowerState == 1 && _PowerIsOn == false)
                    {
                        _LastPowerState = 0;
                        PowerOnGo();                        
                    }
                    else if (_LastPowerState == 2 && _PowerIsOn == true)
                    {
                        _LastPowerState = 0;
                        PowerOffGo();
                    }
                    else
                    {
                        _LastPowerState = 0;
                    }
                }
                _PowerMutex.ReleaseMutex();
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
            SendCommand("PWR?");
        }

        public void VideoMuteOn()
        {
            SendCommand("MUTE ON");
        }

        public void VideoMuteOff()
        {
            SendCommand("MUTE OFF");
        }

        public void VideoMuteGet()
        {
            SendCommand("MUTE?");
        }

        public void LampHoursGet()
        {
            SendCommand("LAMP?");
        }

		public void InputHdmi1()
		{
            SendCommand("SOURCE 30");
		}

        public void InputHdmi2()
        {
            SendCommand("SOURCE A0");
        }

		public void InputNetwork()
		{
            SendCommand("SOURCE 80");
        }

        public void InputVga()
        {
            SendCommand("SOURCE 11");
        }

        public void InputGet()
        {
            SendCommand("SOURCE?");
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