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

        CommunicationGather PortGather;
        RoutingInputPort _CurrentInputPort;

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
            PortGather = new CommunicationGather(Communication, '\x0D');
            PortGather.IncludeDelimiter = false;
            PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(Communication_TextReceived);

            LampHoursFeedback = new IntFeedback(() => { return _LampHours; });
            VideoMuteIsOnFeedback = new BoolFeedback(() => { return _VideoMuteIsOn; });

            WarmupTime = 30000;
            CooldownTime = 20000;

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 2000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.HdmiIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi), this), "30");

            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.DviIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Dvi, new Action(InputDvi), this), "A0");

            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.DmIn, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DmCat, new Action(InputNetwork), this), "80");

            AddRoutingInputPort(new RoutingInputPort(RoutingPortNames.VgaIn, eRoutingSignalType.Video,
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

	    public override FeedbackCollection<Feedback> Feedbacks
		{
			get
			{
				var list = base.Feedbacks;
				list.AddRange(new List<Feedback>
				{
                    CurrentInputFeedback, IsWarmingUpFeedback, IsCoolingDownFeedback, VideoMuteIsOnFeedback, LampHoursFeedback
				});
				return list;
			}
		}

        /// <summary>
        /// /
        /// </summary>
        /// <param name="sender"></param>
        void Communication_TextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            try
            {
                Debug.Console(1, this, "Feedback: {0}", e.Text);
            }
            catch (Exception err)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", err);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendCommand(string cmd)
        {
            Communication.SendText(cmd + "\x0D");
        }

        /// <summary>
        /// 
        /// </summary>
        public void StatusGet()
        {
            SendCommand("PWR?");
            SendCommand("LAMP?");
            SendCommand("MUTE?");
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PowerOn()
		{
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Powering On Display");

            SendCommand("PWR ON");
			if (!PowerIsOnFeedback.BoolValue && !_IsWarmingUp && !_IsCoolingDown)
			{
				_IsWarmingUp = true;
				IsWarmingUpFeedback.FireUpdate();
				// Fake power-up cycle
				WarmupTimer = new CTimer(o =>
					{
						_IsWarmingUp = false;
						_PowerIsOn = true;
						IsWarmingUpFeedback.FireUpdate();
						PowerIsOnFeedback.FireUpdate();
					}, WarmupTime);
			}
		}

        /// <summary>
        /// 
        /// </summary>
		public override void PowerOff()
		{
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Powering Off Display");

            if (!_IsWarmingUp && !_IsCoolingDown) // PowerIsOnFeedback.BoolValue &&
			{
                //Send(PowerOffCmd);
                SendCommand("PWR OFF");
				_IsCoolingDown = true;
				_PowerIsOn = false;
				PowerIsOnFeedback.FireUpdate();
				IsCoolingDownFeedback.FireUpdate();
				// Fake cool-down cycle
				CooldownTimer = new CTimer(o =>
					{
						_IsCoolingDown = false;
						IsCoolingDownFeedback.FireUpdate();
					}, CooldownTime);
			}
		}		
		
		public override void PowerToggle()
		{
			if (PowerIsOnFeedback.BoolValue && !IsWarmingUpFeedback.BoolValue)
				PowerOff();
			else if (!PowerIsOnFeedback.BoolValue && !IsCoolingDownFeedback.BoolValue)
				PowerOn();
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

		public void InputHdmi()
		{
            SendCommand("SOURCE 30");
		}

        public void InputDvi()
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