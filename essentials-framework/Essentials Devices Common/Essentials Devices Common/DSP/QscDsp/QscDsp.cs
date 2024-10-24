﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Devices;

namespace QscQsysDspPlugin
{
	/// <summary>
	/// DSP Device 
	/// </summary>
	/// <remarks>
	/// Questions:
	/// 1. When subscribing, jsut use the Instance ID for custom name?
	/// 2. Verbose on subscription?
	/// 
	/// - Example subscription feedback responses:
	/// ! "publishToken":"name" "value":-77.0
	/// ! "myLevelName" -77
	/// </remarks>
    public class QscDsp : EssentialsBridgeableDevice, IOnline, ICommunicationMonitor
	{
		/// <summary>
		/// Communication object
		/// </summary>
		public IBasicCommunication Communication { get; private set; }

		/// <summary>
		/// Gather object
		/// </summary>
		public CommunicationGather PortGather { get; private set; }

		/// <summary>
		/// Communication monitor object
		/// </summary>
		public StatusMonitorBase CommunicationMonitor { get; private set; }

		public Dictionary<string, QscDspLevelControl> LevelControlPoints { get; private set; }
		public Dictionary<string, QscDspDialer> Dialers { get; set; }
		public Dictionary<string, QscDspCamera> Cameras { get; set; }
		public List<QscDspPresets> PresetList = new List<QscDspPresets>();

		DeviceConfig _Dc;

		CrestronQueue CommandQueue;

		bool CommandQueueInProgress = false;
		uint HeartbeatTracker = 0;
		public bool ShowHexResponse { get; set; }
		
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="key">String</param>
		/// <param name="name">String</param>
		/// <param name="comm">IBasicCommunication</param>
		/// <param name="dc">DeviceConfig</param>
		public QscDsp(string key, string name, IBasicCommunication comm, DeviceConfig dc)
			: base(key, name)
		{
			_Dc = dc;
			var props = JsonConvert.DeserializeObject<QscDspPropertiesConfig>(dc.Properties.ToString());
			Debug.Console(2, this, "Made it to device constructor");

			CommandQueue = new CrestronQueue(100);
			Communication = comm;
			var socket = comm as ISocketStatus;
			if (socket != null)
			{
				// This instance uses IP control
				socket.ConnectionChange += socket_ConnectionChange;
			}
			else
			{
				// This instance uses RS-232 control
			}

			PortGather = new CommunicationGather(Communication, "\x0a");
			PortGather.LineReceived += this.Port_LineReceived;

			// Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
			CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, CheckSubscriptions);

			LevelControlPoints = new Dictionary<string, QscDspLevelControl>();
			Dialers = new Dictionary<string, QscDspDialer>();
			Cameras = new Dictionary<string, QscDspCamera>();
			CreateDspObjects();
		}

		/// <summary>
		/// CustomActivate Override
		/// </summary>
		/// <returns></returns>
		public override bool CustomActivate()
		{
			Communication.Connect();
			CommunicationMonitor.StatusChange += 
                (o, a) => Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);

			CrestronConsole.AddNewConsoleCommand(SendLine, "send" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			CrestronConsole.AddNewConsoleCommand(s => Communication.Connect(), "con" + Key, "", ConsoleAccessLevelEnum.AccessOperator);
			return true;
		}

		void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
		{
			if (e.Client.IsConnected)
			{
				SubscribeToAttributes();
			}
			else
			{
				// Cleanup items from this session
				CommandQueue.Clear();
				CommandQueueInProgress = false;
			}
		}

        private string FormatTag(string prefix, string tag)
        {
            if (prefix == null)
                prefix = "";
            if (tag == null)
                return null;
            else
                return string.Format("{0}{1}", prefix, tag);
        }

		public void CreateDspObjects()
		{

			var props = JsonConvert.DeserializeObject<QscDspPropertiesConfig>(_Dc.Properties.ToString());

			LevelControlPoints.Clear();
			PresetList.Clear();
			Dialers.Clear();
			Cameras.Clear();

			// Check for prefix
			string prefix = "";
			if (props.Prefix != null) { prefix = props.Prefix; }

			if (props.LevelControlBlocks != null)
			{
				foreach (KeyValuePair<string, QscDspLevelControlBlockConfig> block in props.LevelControlBlocks)
				{
					string key = string.Format("{0}{1}", prefix, block.Key);
					var value = block.Value;
                    value.LevelInstanceTag = FormatTag(prefix, value.LevelInstanceTag);
					value.MuteInstanceTag = FormatTag(prefix, value.MuteInstanceTag);

					this.LevelControlPoints.Add(key, new QscDspLevelControl(key, value, this));
					Debug.Console(2, this, "Added LevelControlPoint {0} LevelTag: {1} MuteTag: {2}", key, value.LevelInstanceTag, value.MuteInstanceTag);
				}
			}
			if (props.Presets != null)
			{
				foreach (KeyValuePair<string, QscDspPresets> preset in props.Presets)
				{
					var value = preset.Value;
                    value.Preset = string.Format("{0}{1}", prefix, value.Preset);
					this.addPreset(value);
					Debug.Console(2, this, "Added Preset {0} {1}", value.Label, value.Preset);
				}
			}
			if (props.CameraControlBlocks != null)
			{
				foreach (KeyValuePair<string, QscDspCameraConfig> camera in props.CameraControlBlocks)
				{
					var value = camera.Value;
					var key = camera.Key;

                    value.PanLeftTag = FormatTag(prefix, value.PanLeftTag);
                    value.PanRightTag = FormatTag(prefix, value.PanRightTag);
                    value.TiltUpTag = FormatTag(prefix, value.TiltUpTag);
                    value.TiltDownTag = FormatTag(prefix, value.TiltDownTag);
                    value.ZoomInTag = FormatTag(prefix, value.ZoomInTag);
                    value.ZoomOutTag = FormatTag(prefix, value.ZoomOutTag);
                    value.PresetBankTag = FormatTag(prefix, value.PresetBankTag);
                    value.Privacy = FormatTag(prefix, value.Privacy);
                    value.OnlineStatus = FormatTag(prefix, value.OnlineStatus);
					foreach (var preset in value.Presets)
					{
                        value.Presets[preset.Key].Bank = FormatTag(prefix, value.Presets[preset.Key].Bank);
					}

					Cameras.Add(key, new QscDspCamera(this, key, key, value));
					Debug.Console(2, this, "Added Camera {0}\n {1}", key, value);

				}
			}
			if (props.DialerControlBlocks != null)
			{
				foreach (KeyValuePair<string, QscDialerConfig> dialerConfig in props.DialerControlBlocks)
				{
					var value = dialerConfig.Value;
					var key = dialerConfig.Key;
					key = string.Format("{0}{1}", prefix, key);
                    value.AutoAnswerTag = FormatTag(prefix, value.AutoAnswerTag);
                    value.CallStatusTag = FormatTag(prefix, value.CallStatusTag);
                    value.ConnectTag = FormatTag(prefix, value.ConnectTag);
                    value.DialStringTag = FormatTag(prefix, value.DialStringTag);
                    value.DisconnectTag = FormatTag(prefix, value.DisconnectTag);
                    value.DoNotDisturbTag = FormatTag(prefix, value.DoNotDisturbTag);
                    value.HookStatusTag = FormatTag(prefix, value.HookStatusTag);
                    value.IncomingCallRingerTag = FormatTag(prefix, value.IncomingCallRingerTag);
                    value.Keypad0Tag = FormatTag(prefix, value.Keypad0Tag);
                    value.Keypad1Tag = FormatTag(prefix, value.Keypad1Tag);
                    value.Keypad2Tag = FormatTag(prefix, value.Keypad2Tag);
                    value.Keypad3Tag = FormatTag(prefix, value.Keypad3Tag);
                    value.Keypad4Tag = FormatTag(prefix, value.Keypad4Tag);
                    value.Keypad5Tag = FormatTag(prefix, value.Keypad5Tag);
                    value.Keypad6Tag = FormatTag(prefix, value.Keypad6Tag);
                    value.Keypad7Tag = FormatTag(prefix, value.Keypad7Tag);
                    value.Keypad8Tag = FormatTag(prefix, value.Keypad8Tag);
                    value.Keypad9Tag = FormatTag(prefix, value.Keypad9Tag);
                    value.KeypadBackspaceTag = FormatTag(prefix, value.KeypadBackspaceTag);
                    value.KeypadClearTag = FormatTag(prefix, value.KeypadClearTag);
                    value.KeypadPoundTag = FormatTag(prefix, value.KeypadPoundTag);
                    value.KeypadStarTag = FormatTag(prefix, value.KeypadStarTag);
					this.Dialers.Add(key, new QscDspDialer(value, this));
					Debug.Console(2, this, "Added Dialer {0}\n {1}", key, value);

				}
			}
			SubscribeToAttributes();
		}

		/// <summary>
		/// Checks the subscription health, should be called by comm monitor only. If no heartbeat has been detected recently, will resubscribe and log error.
		/// </summary>
		void CheckSubscriptions()
		{
			HeartbeatTracker++;
			SendLine("cgp 2");
			CrestronEnvironment.Sleep(1000);

			if (HeartbeatTracker > 0)
			{
				Debug.Console(1, this, "Heartbeat missed, count {0}", HeartbeatTracker);
				if (HeartbeatTracker % 5 == 0)
				{
					Debug.Console(1, this, "Heartbeat missed 5 times, subscriptions lost? Resubscribing now");
					if (HeartbeatTracker == 5)
						Debug.LogError(Debug.ErrorLogLevel.Warning, "Heartbeat missed 5 times - subscriptions lost? Attempting resubscribe.");
					SubscribeToAttributes();
				}
			}
			else
			{
				Debug.Console(1, this, "Heartbeat okay");
			}
		}

		/// <summary>
		/// Initiates the subscription process to the DSP
		/// </summary>
		void SubscribeToAttributes()
		{
			// Change Group destroy
			SendLine("cgd 1");
			SendLine("cgd 2");

			// Change Group create
			SendLine("cgc 1");
			SendLine("cgc 2");

			// Change group subscribe to feedback with no ack (updates every 1000 ms)
			SendLine("cgsna 1 1000");

			foreach (KeyValuePair<string, QscDspLevelControl> level in LevelControlPoints)
			{
				level.Value.Subscribe();
			}

			foreach (var dialer in Dialers)
			{
				dialer.Value.Subscribe();
			}

			foreach (var camera in Cameras)
			{
				camera.Value.Subscribe();
			}

			if (CommunicationMonitor != null)
			{
				CommunicationMonitor.Start();
			}

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Handles a response message from the DSP
		/// </summary>
		/// <param name="dev"></param>
		/// <param name="args"></param>
		void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
		{
			Debug.Console(2, this, "RX: '{0}'", args.Text);
			try
			{
				if (args.Text.EndsWith("cgpa\r"))
				{
					Debug.Console(1, this, "Found poll response");
					HeartbeatTracker = 0;
				}
				if (args.Text.IndexOf("sr ") > -1)
				{
				}
				else if (args.Text.IndexOf("cv") > -1)
				{
                    var changeMessage = Regex.Split(args.Text, " (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");   //Splits by space unless enclosed in double quotes using look ahead method: https://stackoverflow.com/questions/18893390/splitting-on-comma-outside-quotes

                    string changedInstance = changeMessage[1].Replace("\"", "");
					Debug.Console(1, this, "cv parse Instance: {0}", changedInstance);
					bool foundItFlag = false;
					foreach (KeyValuePair<string, QscDspLevelControl> controlPoint in LevelControlPoints)
					{
                        if (changedInstance == controlPoint.Value.LevelInstanceTag)
						{
							controlPoint.Value.ParseSubscriptionMessage(changedInstance, changeMessage[4], changeMessage[3]);
							foundItFlag = true;
							return;
						}

                        else if (changedInstance == controlPoint.Value.MuteInstanceTag)
						{
							controlPoint.Value.ParseSubscriptionMessage(changedInstance, changeMessage[2].Replace("\"", ""), null);
							foundItFlag = true;
							return;
						}

					}
					if (!foundItFlag)
					{
						foreach (var dialer in Dialers)
						{
							PropertyInfo[] properties = dialer.Value.Tags.GetType().GetCType().GetProperties();
							foreach (var prop in properties)
							{
								var propValue = prop.GetValue(dialer.Value.Tags, null) as string;
                                if (changedInstance == propValue)
								{
									if (changeMessage[2].Contains("Dialing") || changeMessage[2].Contains("Connected"))
									{
										dialer.Value.ParseSubscriptionMessage(changedInstance, changeMessage[2].Replace("\"", "") + " " + changeMessage[4].Replace("\"", ""));
									}
									else
									{
										dialer.Value.ParseSubscriptionMessage(changedInstance, changeMessage[2].Replace("\"", ""));
									}
									foundItFlag = true;
									return;
								}
							}
							if (foundItFlag)
							{
								return;
							}
						}
					}
					if (!foundItFlag)
					{
						foreach (var camera in Cameras)
						{
							Debug.Console(1, this, "DSP Camera Status Compare: {0} ==? {1}", changedInstance, camera.Value.Config.OnlineStatus);
							if (changedInstance == camera.Value.Config.OnlineStatus)
							{
								camera.Value.ParseSubscriptionMessage(changedInstance, changeMessage[2].Replace("\"", ""), null);
								foundItFlag = true;
								return;
							}
						}
						if (foundItFlag)
						{
							return;
						}
					}

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
			Debug.Console(1, this, "TX: '{0}'", s);
			Communication.SendText(s + "\x0a");
		}

		/// <summary>
		/// Adds a command from a child module to the queue
		/// </summary>
		/// <param name="commandToEnqueue">Command object from child module</param>
		public void EnqueueCommand(QueuedCommand commandToEnqueue)
		{
			CommandQueue.Enqueue(commandToEnqueue);
			//Debug.Console(1, this, "Command (QueuedCommand) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", commandToEnqueue.Command, CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Adds a raw string command to the queue
		/// </summary>
		/// <param name="command"></param>
		public void EnqueueCommand(string command)
		{
			CommandQueue.Enqueue(command);
			//Debug.Console(1, this, "Command (string) Enqueued '{0}'.  CommandQueue has '{1}' Elements.", command, CommandQueue.Count);

			if (!CommandQueueInProgress)
				SendNextQueuedCommand();
		}

		/// <summary>
		/// Sends the next queued command to the DSP
		/// </summary>
		void SendNextQueuedCommand()
		{
			if (Communication.IsConnected && !CommandQueue.IsEmpty)
			{
				CommandQueueInProgress = true;

				if (CommandQueue.Peek() is QueuedCommand)
				{
					QueuedCommand nextCommand = new QueuedCommand();

					nextCommand = (QueuedCommand)CommandQueue.Peek();

					SendLine(nextCommand.Command);
				}
				else
				{
					string nextCommand = (string)CommandQueue.Peek();

					SendLine(nextCommand);
				}
			}

		}

		/// <summary>
		/// Runs the preset with the number provided
		/// </summary>
		/// <param name="n">ushort</param>
		public void RunPresetNumber(ushort n)
		{
			RunPreset(PresetList[n].Preset);
		}

		/// <summary>
		/// Adds a presst
		/// </summary>
		/// <param name="s">QscDspPresets</param>
		public void addPreset(QscDspPresets s)
		{
			PresetList.Add(s);
		}

		/// <summary>
		/// Sends a command to execute a preset
		/// </summary>
		/// <param name="name">Preset Name</param>
		public void RunPreset(string name)
		{
			SendLine(string.Format("ssl {0}", name));
			SendLine("cgp 1");
		}

		/// <summary>
		/// Queues Commands
		/// </summary>
		public class QueuedCommand
		{
			public string Command { get; set; }
			public string AttributeCode { get; set; }
			public QscDspControlPoint ControlPoint { get; set; }
		}

		#region IBridge Members

		/// <summary>
		/// Link to API
		/// </summary>
		/// <param name="trilist">BasicTriList</param>
		/// <param name="joinStart">uint</param>
		/// <param name="joinMapKey">string</param>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			this.LinkToApiExt(trilist, joinStart, joinMapKey, bridge);
		}

		#endregion

	    public BoolFeedback IsOnline { get { return CommunicationMonitor.IsOnlineFeedback; } }
	}
}