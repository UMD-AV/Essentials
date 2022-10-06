using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Bridges;

namespace ExtronDmp
{
	/// <summary>
	/// DSP Device 
	/// </summary>
	/// <remarks>
	/// </remarks>
	public class ExtronDmp : EssentialsBridgeableDevice, ICommunicationMonitor, IOnline
	{
        const string connectedPrompt = "(c) Copyright";
        const string passwordPrompt = "Password:";

		/// <summary>
		/// Communication object
		/// </summary>
        private readonly IBasicCommunication _comm;

		/// <summary>
		/// Communication monitor object
		/// </summary>
        public readonly GenericCommunicationMonitor _commMonitor;        

        public CommunicationGather PortGather { get; private set; }
		public Dictionary<int, ExtronDmpLevelControl> LevelControlPoints { get; private set; }
		public List<ExtronDmpPreset> PresetList = new List<ExtronDmpPreset>();
		public Dictionary<ushort, ExtronDmpDialer> Dialers { get; private set; }

		private readonly ExtronDmpConfig _config;
		private uint HeartbeatTracker = 0;
		public bool ShowHexResponse { get; set; }
        public string DeviceId { get; set; }
        private bool needToInitialize = true;
        private string version = "";
        private string model = "";

	    public readonly string Password;
		
		
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">String</param>
		/// <param name="name">String</param>
		/// <param name="comm">IBasicCommunication</param>
		/// <param name="dc">DeviceConfig</param>
		public ExtronDmp(string key, string name, IBasicCommunication comm, ExtronDmpConfig config)
			: base(key, name)
		{
			Debug.Console(2, this, "Creating Extron Dmp DSP Instance");
            _config = config;

            DeviceId = "30";
            if (!string.IsNullOrEmpty(_config.DeviceId))
            {
                DeviceId = _config.DeviceId;
            }

			_comm = comm;

			PortGather = new CommunicationGather(_comm, "\x0a");
            PortGather.IncludeDelimiter = false;
			PortGather.LineReceived += this.ResponseReceived;

			_commMonitor = new GenericCommunicationMonitor(this, _comm, 30000, 121000, 301000, CheckComms);
            _commMonitor.StatusChange += new EventHandler<MonitorStatusChangeEventArgs>(ConnectionChange);

			LevelControlPoints = new Dictionary<int, ExtronDmpLevelControl>();
			Dialers = new Dictionary<ushort, ExtronDmpDialer>();

			CreateDspObjects();
			
		    Password = _config.Control.TcpSshProperties.Password;
		}

		private void ConnectionChange(object sender, MonitorStatusChangeEventArgs e)
		{
            Debug.Console(2, this, "Communication monitor state: {0}", e.Status);
		}

		public override void Initialize()
		{
			_comm.Connect();
			_commMonitor.Start();
		}
		public void CreateDspObjects()
		{
			LevelControlPoints.Clear();
			PresetList.Clear();

			if (_config.LevelControlBlocks != null)
			{
				foreach (KeyValuePair<string, ExtronDmpControlBlockConfig> block in _config.LevelControlBlocks)
				{
                    if (block.Value.Disabled == true)
                    {
                        Debug.Console(2, this, "Skipping disabled LevelControlBlock {0}", block.Key);
                    }
                    else
					{
                        var control = new ExtronDmpLevelControl(block.Key, block.Value, this);
                        if (block.Value.ControlId != null)
                        {
                            this.LevelControlPoints.Add(block.Value.ControlId.Value, control);
                            Debug.Console(2, this, "Added ControlId for key {0}", block.Key);
                        }
                        else if (block.Value.LevelGroup != null)
                        {
                            this.LevelControlPoints.Add(block.Value.LevelGroup.Value, control);
                            Debug.Console(2, this, "Added LevelGroup for key {0}", block.Key);
                        }
                        else if (block.Value.MuteGroup != null)
                        {
                            this.LevelControlPoints.Add(block.Value.MuteGroup.Value, control);
                            Debug.Console(2, this, "Added MuteGroup for key {0}", block.Key);
                        }
					}
				}
			}
			if (_config.Presets != null)
			{
				foreach (KeyValuePair<string, ExtronDmpPreset> preset in _config.Presets)
				{
					this.addPreset(preset.Value);
					Debug.Console(2, this, "Added Preset {0} {1}", preset.Value.Label, preset.Value.id);
				}
			}
			if (_config.DialerControlBlocks != null)
			{
				foreach (KeyValuePair<string, ExtronDmpDialerConfig> dialerConfig in _config.DialerControlBlocks)
				{
					Dialers.Add(dialerConfig.Value.LineNumber, new ExtronDmpDialer(dialerConfig.Value, this));
				}
			}
		}

		/// <summary>
		/// Checks the comm health, should be called by comm monitor only. If no heartbeat has been detected recently, will clear the queue and log an error.
		/// </summary>
		private void CheckComms()
		{
			HeartbeatTracker++;
			SendLine("Q");
			CrestronEnvironment.Sleep(1000);

			if (HeartbeatTracker > 0)
			{
				Debug.Console(1, this, "Heartbeat missed, count {0}", HeartbeatTracker);

				if (HeartbeatTracker == 5)
					Debug.LogError(Debug.ErrorLogLevel.Warning, "Heartbeat missed 5 times");
			}
			else
			{
				Debug.Console(2, this, "Heartbeat okay");
			}
		}

		/// <summary>
		/// Initiates the subscription process to the DSP
		/// </summary>
		void InitializeDspObjects()
		{
            SendLine(string.Format("{0}3CV\x0D", '\x1B'));  //Set verbose mode 3
            CrestronEnvironment.Sleep(250);
            foreach (var channel in LevelControlPoints)
            {
                if (channel.Value.HasLevel)
                {
                    channel.Value.GetCurrentMinMax();
                    CrestronEnvironment.Sleep(250);
                    channel.Value.GetCurrentGain();
                    CrestronEnvironment.Sleep(250);
                }
                if (channel.Value.HasMute)
                {
                    channel.Value.GetCurrentMute();
                    CrestronEnvironment.Sleep(250);
                }
            }
			foreach (var line in Dialers)
			{
				line.Value.Init();
			}
		}

		/// <summary>
		/// Handles a response message from the DSP
		/// </summary>
		/// <param name="dev"></param>
		/// <param name="args"></param>
		void ResponseReceived(object dev, GenericCommMethodReceiveTextArgs args)
		{
            HeartbeatTracker = 0;
            if (args.Text.Length <= 1)
            {
                return;
            }
			try
			{
                Debug.Console(1, this, "Rx: {0}", args.Text);

			    if (args.Text.StartsWith(passwordPrompt))
			    {
                    SendLine(Password);
			    }
                else if (args.Text.StartsWith(connectedPrompt))
                {
                    needToInitialize = true;
                    string[] startPrompt = args.Text.Split(',');
                    if (startPrompt.Length == 5)
                    {
                        version = startPrompt[3].Trim().Substring(1);
                        model = startPrompt[2].Trim();
                        Debug.Console(1, this, "Found start prompt. Version: {0} Model: {1}", version, model);
                    }
                }
                else if (args.Text.StartsWith("Ver") || (args.Text.StartsWith(version) && version.Length > 2))
                {
                    if (needToInitialize)
                    {
                        needToInitialize = false;
                        InitializeDspObjects();
                    }
                }
			    else if (args.Text.StartsWith("Grpm")) // If Group
                {
                    // example: GrpmD1*00293
                    // example: command = 'D'
                    // example: group = 1
					string command = args.Text.Substring(4,1);
                    int starPos = args.Text.IndexOf('*');
                    int group = int.Parse(args.Text.Substring(5, starPos - 5));

                    if (LevelControlPoints.ContainsKey(group))
                    {
                        //This is for level feedback
                        LevelControlPoints[group].ParseResponse(command, args.Text);
                    }
                    else
                    {
                        //This is for mute feedback
                        foreach (var x in LevelControlPoints.Where(n => n.Value.MuteGroup == group))
                        {
                            x.Value.ParseResponse(command, args.Text);
                        }
                    }
                }
                else if (args.Text.StartsWith("Ds")) // If trim/gain
                {
                    // example: DsG40001*2288
                    // example: command = 'G'
                    // example: controlId = 40001
                    string command = args.Text.Substring(2, 1);
                    int starPos = args.Text.IndexOf('*');    
                    int controlId = int.Parse(args.Text.Substring(3, starPos - 3));

                    if (LevelControlPoints.ContainsKey(controlId))
                    {
                        LevelControlPoints[controlId].ParseResponse(command, args.Text);
                    }
                }
				else if (args.Text.ToUpper().Contains("VOIPLS"))
				{
					//VoipLS1,1,0,0,0,0,0,0,0
					var line = args.Text.ToUpper()[6].ToString();
					var status = args.Text.ToUpper()[8].ToString();
					Debug.Console(1, this, "Found a LINESTATUS response line: {0}, status: {1}", line, status);
					Dialers[ushort.Parse(line)].SetLineStatus((ExtronDmpDialer.ELineStatus)(ushort.Parse(status)));						
				}
			}
			catch (Exception e)
			{
				if (Debug.Level == 2)
					Debug.Console(2, this, "Error parsing response: '{0}'\n{1}", args.Text, e);
			}
		}

		/// <summary>
		/// Sends a command to the DSP (with delimiter appended)
		/// </summary>
		/// <param name="s">Command to send</param>
		public void SendLine(string s)
		{
			_comm.SendText(s + "\x0D");
			Debug.Console(2, this, "Send: '{0}'", s); 
		}

        /// <summary>
        /// Runs the preset with the number provided
        /// </summary>
        /// <param name="n">ushort</param>
        public void RunPreset(ushort preset)
        {
			Debug.Console(1, this, "RunPreset: '{0}'", preset);
			
            if (0 < preset && preset <= PresetList.Count && PresetList[preset-1] != null)
            {
				var p = PresetList[preset-1];
				if (p.isMacro)
				{
					SendLine(string.Format("{0}R{1}MCRO", '\x1B', preset));
				}
				else
				{
					SendLine(string.Format("{0}.", preset));
				}
            }
        }

		/// <summary>
		/// Runs the preset object provided
		/// </summary>
		/// <param name="n">ConvergeProDspPreset</param>
		public void RunPreset(ExtronDmpPreset preset)
		{
	        RunPreset(preset.id);
		}

		/// <summary>
		/// Adds a presst
		/// </summary>
		/// <param name="s">ConvergeProDspPresets</param>
		public void addPreset(ExtronDmpPreset s)
		{
			PresetList.Add(s);
		}

		/// <summary>
		/// Queues Commands
		/// </summary>
		public class QueuedCommand
		{
			public string Command { get; set; }
			public string AttributeCode { get; set; }
		}

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new ExtronDmpJoinMap(joinStart);

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            // from Plugin > to SiMPL
            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            ushort channelIndex = 1;
            foreach (var channel in LevelControlPoints)
            {
                var genericChannel = channel.Value as IBasicVolumeWithFeedback;
                // from SiMPL > to Plugin
                trilist.StringInput[joinMap.ChannelName.JoinNumber + channelIndex].StringValue = channel.Value.LevelCustomName;
                trilist.UShortInput[joinMap.ChannelType.JoinNumber + channelIndex].UShortValue = (ushort)channel.Value.Type;
                trilist.BooleanInput[joinMap.ChannelVisible.JoinNumber + channelIndex].BoolValue = true;
                // from Plugin > to SiMPL
                genericChannel.MuteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.ChannelMuteToggle.JoinNumber + channelIndex]);
                genericChannel.MuteFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.ChannelMuteOff.JoinNumber + channelIndex]);
                genericChannel.VolumeLevelFeedback.LinkInputSig(trilist.UShortInput[joinMap.ChannelVolume.JoinNumber + channelIndex]);
                // from SiMPL > to Plugin
                trilist.SetSigTrueAction(joinMap.ChannelMuteToggle.JoinNumber + channelIndex, () => genericChannel.MuteToggle());
                trilist.SetSigTrueAction(joinMap.ChannelMuteOn.JoinNumber + channelIndex, () => genericChannel.MuteOn());
                trilist.SetSigTrueAction(joinMap.ChannelMuteOff.JoinNumber + channelIndex, () => genericChannel.MuteOff());
                // from SiMPL > to Plugin
                trilist.SetBoolSigAction(joinMap.ChannelVolumeUp.JoinNumber + channelIndex, b => genericChannel.VolumeUp(b));
                trilist.SetBoolSigAction(joinMap.ChannelVolumeDown.JoinNumber + channelIndex, b => genericChannel.VolumeDown(b));
                // from SiMPL > to Plugin
                trilist.SetUShortSigAction(joinMap.ChannelVolume.JoinNumber + channelIndex, u => genericChannel.SetVolume(u));
                channelIndex++;
            }

			// VoIP Dialer
			uint lineOffset = 0;
			foreach (var line in Dialers)
			{
				var dialer = line;

				var dialerLineOffset = lineOffset;
				Debug.Console(0, "AddingDialerBridge {0} {1} Offset", dialer.Key, dialerLineOffset);

				// from SiMPL > to Plugin
				for (var i = 0; i < joinMap.KeyPadNumeric.JoinSpan; i++)
				{
					var tempi = i;
					trilist.SetSigTrueAction((joinMap.KeyPadNumeric.JoinNumber + (uint)i + dialerLineOffset), () => Dialers[dialer.Key].SendKeypad((ExtronDmpDialer.EKeypadKeys)(tempi)));
				}
				trilist.SetSigTrueAction((joinMap.KeyPadStar.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(ExtronDmpDialer.EKeypadKeys.Star));
				trilist.SetSigTrueAction((joinMap.KeyPadPound.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(ExtronDmpDialer.EKeypadKeys.Pound));
				trilist.SetSigTrueAction((joinMap.KeyPadClear.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(ExtronDmpDialer.EKeypadKeys.Clear));
				trilist.SetSigTrueAction((joinMap.KeyPadBackspace.JoinNumber + dialerLineOffset), () => dialer.Value.SendKeypad(ExtronDmpDialer.EKeypadKeys.Backspace));
				// from SiMPL > to Plugin
				trilist.SetSigTrueAction(joinMap.KeyPadDial.JoinNumber + dialerLineOffset, () => dialer.Value.Dial());
				trilist.SetStringSigAction(joinMap.KeyPadDial.JoinNumber + dialerLineOffset, dialer.Value.Dial);
				trilist.SetSigTrueAction(joinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbToggle());
				trilist.SetSigTrueAction(joinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbOn());
				trilist.SetSigTrueAction(joinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset, () => dialer.Value.DoNotDisturbOff());
				trilist.SetSigTrueAction(joinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerToggle());
				trilist.SetSigTrueAction(joinMap.AutoAnswerOn.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerOn());
				trilist.SetSigTrueAction(joinMap.AutoAnswerOff.JoinNumber + dialerLineOffset, () => dialer.Value.AutoAnswerOff());
				trilist.SetSigTrueAction(joinMap.EndCall.JoinNumber + dialerLineOffset, () => dialer.Value.EndAllCalls());

				// from SIMPL > to Plugin
				trilist.SetStringSigAction(joinMap.KeyPadDial.JoinNumber + dialerLineOffset, directDialString => dialer.Value.Dial(directDialString));

				// from Plugin > to SiMPL
				dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbToggle.JoinNumber + dialerLineOffset]);
				dialer.Value.DoNotDisturbFeedback.LinkInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOn.JoinNumber + dialerLineOffset]);
				dialer.Value.DoNotDisturbFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.DoNotDisturbOff.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
				dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerToggle.JoinNumber + dialerLineOffset]);
				dialer.Value.AutoAnswerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoAnswerOn.JoinNumber + dialerLineOffset]);
				dialer.Value.AutoAnswerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoAnswerOff.JoinNumber + dialerLineOffset]);
				dialer.Value.CallerIdNumberFeedback.LinkInputSig(trilist.StringInput[joinMap.CallerIdNumberFb.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
				dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.KeyPadDial.JoinNumber + dialerLineOffset]);
				dialer.Value.OffHookFeedback.LinkInputSig(trilist.BooleanInput[joinMap.OffHook.JoinNumber + dialerLineOffset]);
				dialer.Value.OffHookFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.OnHook.JoinNumber + dialerLineOffset]);
				dialer.Value.DialStringFeedback.LinkInputSig(trilist.StringInput[joinMap.DialString.JoinNumber + dialerLineOffset]);

				// from Plugin > to SiMPL
				dialer.Value.IncomingCallFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IncomingCall.JoinNumber + dialerLineOffset]);

				lineOffset = lineOffset + 50;
			}
            foreach (var preset in PresetList)
            {
				ushort x = preset.id;
                var thisPreset = preset as ExtronDmpPreset;
                if (x > 100)
                {
                    break;
                }
                // from SiMPL > to Plugin

                trilist.StringInput[joinMap.PresetName.JoinNumber + x].StringValue = preset.Label;
                trilist.SetUShortSigAction(joinMap.PresetRecall.JoinNumber, u => RunPreset(u));
                trilist.SetSigTrueAction(joinMap.PresetRecall.JoinNumber + x, () => RunPreset(thisPreset));
            }
        }

	    public BoolFeedback IsOnline { get { return _commMonitor.IsOnlineFeedback; } }
	    public StatusMonitorBase CommunicationMonitor 
        {
	        get { return _commMonitor; }
	    }
	}
}