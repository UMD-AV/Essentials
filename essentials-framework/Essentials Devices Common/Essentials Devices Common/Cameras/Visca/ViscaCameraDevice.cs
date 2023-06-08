using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;

namespace ViscaCameraPlugin
{
	public class ViscaCameraDevice : EssentialsBridgeableDevice, ICommunicationMonitor
	{
        public StatusMonitorBase CommunicationMonitor { get; private set; }
		private readonly IBasicCommunication _comms;
        private CrestronQueue<ViscaCameraCommand> _commandQueue;
        private CMutex _commandMutex;
        private CTimer _commandTimer;
        private CMutex _feedbackMutex;
        private byte[] _incomingBuffer = { };
        private bool _queueWaiting = false;
        private bool _commandReady = true;
		private readonly bool _commsIsSerial;
		private readonly bool _useHeader;
		private uint _counter = 0;
        private bool _autoTrackingCapable = false;
        private bool _usePresetsForAutoTracking = false;
        private uint _lastCalledPreset = 0;
        private EDirection _moveInProgress = EDirection.Stop;
        private eViscaCameraInquiry _lastInquiry = eViscaCameraInquiry.NoFeedback;

		private readonly ViscaCameraConfig _config;

		private byte _address = 0x81;
        private byte _feedbackAddress = 0x90;
		private const uint AddressMax = 7;

		private readonly long _pollTimeMs = 30000; // 30s
		private readonly long _warningTimeoutMs = 60000; // 60s
		private readonly long _errorTimeoutMs = 180000; // 180s


		private readonly uint _privacyOnPreset;
		private readonly uint _privacyOffPreset;


		private bool _power;
		/// <summary>
		/// Power feedback
		/// </summary>
		public BoolFeedback PowerFeedback { get; private set; }
		/// <summary>
		/// Power property
		/// </summary>
		public bool Power
		{
			get { return _power; }
			set
			{
				if (_power == value) return;
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
        public bool AutoTrackingOn
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
		/// Auto focus feedback
		/// </summary>
		public BoolFeedback AutoFocusFeedback { get; private set; }
		/// <summary>
		/// Auto focus property
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
        public uint ActivePreset
        {
            get { return (uint)_activePreset; }
            set
            {
                if (_activePreset == value) return;
                _activePreset = (int)value;
                ActivePresetFeedback.FireUpdate();
                foreach (var feedback in PresetActiveFeedbacks)
                {
                    feedback.Value.FireUpdate();
                }
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

        public class ViscaCameraCommand
        {
            public eViscaCameraInquiry Command;
            public byte[] Bytes;

            public ViscaCameraCommand(eViscaCameraInquiry command, byte[] bytes)
            {
                Command = command;
                Bytes = bytes;
            }
        }

        /// <summary>
        /// For tracking feedback responses from camera
        /// </summary>
        public enum eViscaCameraInquiry
        {
            PowerOnCmd,
            PowerOffCmd,
            AutoTrackOnCmd,
            AutoTrackOffCmd,
            AutoTrackOnPresetCmd,
            AutoTrackOffPresetCmd,
            PresetRecallCmd,
            PowerInquiry,
            AutoTrackInquiry,
            FocusInquiry,
            PresetInquiry,
            PresetSave,
            NoFeedback
        }

		/// <summary>
		/// Move PTZ direction enumeration
		/// </summary>
		public enum EDirection
		{
			Stop = 0,
			PanLeft = 1,
			PanRight = 2,
			TiltUp = 3,
			TiltDown = 4,
			ZoomIn = 5,
			ZoomOut = 6
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
		/// Monitor status feedback
		/// </summary>
		public IntFeedback MonitorStatusFeedback { get; private set; }

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
		public ViscaCameraDevice(string key, string name, IBasicCommunication comms, ViscaCameraConfig config)
			: base(key, name)
		{
			Debug.Console(0, this, "Constructing new VISCA Camera instance");

			_config = config;

			OnlineFeedback = new BoolFeedback(() => _comms.IsConnected);
            MonitorStatusFeedback = new IntFeedback(() => (int)CommunicationMonitor.Status);
            AutoTrackingCapable = new BoolFeedback(() => _autoTrackingCapable);

			PowerFeedback = new BoolFeedback(() => Power);
			AutoFocusFeedback = new BoolFeedback(() => AutoFocus);
            AutoTrackingOnFeedback = new BoolFeedback(() => AutoTrackingOn);
            PrivacyOnFeedback = new BoolFeedback(() => PrivacyOn);
			PresetCountFeedback = new IntFeedback(() => (int)PresetCount);
			PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();
            PresetActiveFeedbacks = new Dictionary<uint, BoolFeedback>();
            ActivePresetFeedback = new IntFeedback(() => (int)ActivePreset);

            if (_config.AutoTracking == true)
                _autoTrackingCapable = true;

            if (_config.UsePresetsForAutoTracking == true)
                _usePresetsForAutoTracking = true;

			if (_config.PollTimeMs > 0 && _config.PollTimeMs != _pollTimeMs)
				_pollTimeMs = _config.PollTimeMs;

			if (_config.WarningTimeoutMs > 0 && _config.WarningTimeoutMs != _warningTimeoutMs)
				_warningTimeoutMs = _config.WarningTimeoutMs;

			if (_config.ErrorTimeoutMs > 0 && _config.ErrorTimeoutMs != _errorTimeoutMs)
				_errorTimeoutMs = _config.ErrorTimeoutMs;

            if (_config.Address > 0 && _config.Address <= AddressMax && _config.Address != _address)
            {
                _address = Convert.ToByte(0x80 + _config.Address);
                _feedbackAddress = Convert.ToByte((uint)(_config.Address + 8) * 16);
            }

			if (_config.PrivacyOnPreset > 0 && _config.PrivacyOnPreset <= PresetMax)
				_privacyOnPreset = _config.PrivacyOnPreset;

			if (_config.PrivacyOffPreset > 0 && _config.PrivacyOffPreset <= PresetMax)
				_privacyOffPreset = config.PrivacyOffPreset;

			if (_config.Control.Method.ToString().ToLower() == "udp")
				_useHeader = true;

			_comms = comms;
			_comms.BytesReceived += Handle_BytesRecieved;
            CommunicationMonitor = new GenericCommunicationMonitor(this, _comms, _pollTimeMs, _warningTimeoutMs, _errorTimeoutMs, Poll, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            _commandQueue = new CrestronQueue<ViscaCameraCommand>(10);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(readyForNextCommand, Timeout.Infinite);
            _feedbackMutex = new CMutex();

			var socket = _comms as ISocketStatus;
			if (socket != null)
			{
				// device is configured for IP control
				_commsIsSerial = false;
				socket.ConnectionChange += socket_ConnectionChange;
				SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
			}
			else
			{
				// device is configured for RS232 control
				_commsIsSerial = true;
				InitializeCamera();
			}

			InitializePresets(_config.Presets);
		}


		/// <summary>
		/// Use the custom activate to connect the device and start the comms monitor
		/// </summary>
		/// <returns></returns>
		public override bool CustomActivate()
		{
			// Essentials will handle the connect method to the device
			_comms.Connect();
			// Essentials will handle starting the comms monitor
            CommunicationMonitor.Start();

			return base.CustomActivate();
		}


		private void InitializePresets(List<ViscaCameraPresetConfig> presets)
		{
			if (presets == null)
			{
				Debug.Console(0, this, "InitializePresets failed, preset dictionary is null");
				return;
			}
			Debug.Console(0, this, "Intializing presets");

			foreach (var preset in presets)
			{
                var index = preset;
                Debug.Console(0, this, "Preset {0} Name: {1}", index.Index, index.Name);

				if (PresetNameFeedbacks == null)
					PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();

                if (PresetActiveFeedbacks == null)
                    PresetActiveFeedbacks = new Dictionary<uint, BoolFeedback>();

                if (PresetNameFeedbacks.ContainsKey(index.Index))
                    PresetNameFeedbacks[index.Index] = new StringFeedback(() => index.Name);
				else
                    PresetNameFeedbacks.Add(index.Index, new StringFeedback(() => index.Name));

                if (PresetActiveFeedbacks.ContainsKey(index.Index))
                    PresetActiveFeedbacks[index.Index] = new BoolFeedback(() => index.Index == ActivePreset);
                else
                    PresetActiveFeedbacks.Add(index.Index, new BoolFeedback(() => index.Index == ActivePreset));
			}
		}

		#region Overrides of EssentialsBridgeableDevice

		/// <summary>
		/// Link to API method replaces bridge class, this method will be called by the bridge directly
		/// </summary>
		/// <param name="trilist"></param>
		/// <param name="joinStart"></param>
		/// <param name="joinMapKey"></param>
		/// <param name="bridge"></param>
		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new ViscaCameraBridgeJoinMap(joinStart);

			// This adds the join map to the collection on the bridge
			if (bridge != null)
			{
				bridge.AddJoinMap(Key, joinMap);
			}

			var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
			if (customJoins != null)
			{
				joinMap.SetCustomJoinData(customJoins);
			}

			Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
			Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

			// link joins to bridge
			trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

            AutoTrackingCapable.LinkInputSig(trilist.BooleanInput[joinMap.AutoTrackingCapable.JoinNumber]);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			// must null check so LinkToApi doesn't except when the device is TCP or UDP
			if (SocketStatusFeedback != null)
				SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);

			// power on
			trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, () => SetPowerOn());
			PowerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
			// power off
			trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, () => SetPowerOff());
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
                if (_privacyOnPreset != 0)
                {
                    RecallPresetByNumber(_privacyOnPreset);
                }
            });
			trilist.SetBoolSigAction(joinMap.PrivacyOff.JoinNumber, sig =>
            {
                if (_privacyOffPreset != 0)
                {
                    RecallPresetByNumber(_privacyOffPreset);
                }
                else
                {
                    RecallHomePosition();
                }
            });
            PrivacyOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PrivacyOn.JoinNumber]);
            PrivacyOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PrivacyOff.JoinNumber]);
			UpdateFeedbacks();

            // preset saved
            PresetSaved += (o, a) =>
            {
                trilist.BooleanInput[joinMap.PresetSaved.JoinNumber].BoolValue = true;
                new CTimer((object x) => trilist.BooleanInput[joinMap.PresetSaved.JoinNumber].BoolValue = false, 3000);
            };

			// preset - analog recall & save by number
			trilist.SetUShortSigAction(joinMap.PresetRecallByNumber.JoinNumber, value =>
			{
				RecallPresetByNumber(value);
				Debug.Console(1, this, "LinkToApi PresetRecallByNumber[{0}] => RecallPreset({1})", joinMap.PresetRecallByNumber.JoinNumber, value);
			});
			trilist.SetUShortSigAction(joinMap.PresetSaveByNumber.JoinNumber, value =>
			{
				SavePresetByNumber(value);
				Debug.Console(1, this, "LinkToApi PresetSaveByNumber[{0}] => SavePreset({1})", joinMap.PresetSaveByNumber.JoinNumber, value);
			});
            ActivePresetFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresetRecallByNumber.JoinNumber]);

			// preset count feedback
			PresetCountFeedback.LinkInputSig(trilist.UShortInput[joinMap.PresetCount.JoinNumber]);

			foreach (var item in PresetNameFeedbacks)
			{
				// preset number
				var preset = (ushort)item.Key;

				// preset names
				var nameJoin = preset + joinMap.PresetNames.JoinNumber - 1;
				var nameFeedback = item.Value;
				nameFeedback.LinkInputSig(trilist.StringInput[nameJoin]);

				// preset recall
				var recallJoin = preset + joinMap.PresetRecall.JoinNumber - 1;
                trilist.SetSigTrueAction(recallJoin, () =>
                {
                    RecallPresetByNumber(preset);
                    Debug.Console(1, this, "LinkToApi PresetRecall[{0}]: RecallPreset({1})", recallJoin, preset);
                });

				// preset save/store
				var saveJoin = preset + joinMap.PresetSave.JoinNumber - 1;
				trilist.SetSigTrueAction(saveJoin, () =>
				{
					SavePresetByNumber(preset);
					Debug.Console(1, this, "LinkToApi PresetSave[{0}]: SavePreset({1})", saveJoin, preset);
				});
			}

            //Link boolean preset feedback
            foreach (var item in PresetActiveFeedbacks)
            {
                item.Value.LinkInputSig(trilist.BooleanInput[item.Key + joinMap.PresetRecall.JoinNumber - 1]);
            }

			// online status 
			trilist.OnlineStatusChange += (o, a) =>
			{
				if (!a.DeviceOnLine) return;
				trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
				UpdateFeedbacks();
			};
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

			foreach (var item in PresetNameFeedbacks)
				item.Value.FireUpdate();

            foreach (var item in PresetActiveFeedbacks)
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
		}

        private void readyForNextCommand(object o)
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
                                CrestronInvoke.BeginInvoke((obj) =>
                                {
                                    SendBytes(cmd.Bytes);
                                });
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

        public void QueueCommand(byte[] bytes)
        {
            QueueCommand(eViscaCameraInquiry.NoFeedback, bytes);
        }

        public void QueueCommand(eViscaCameraInquiry inquiry, byte[] bytes)
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
                readyForNextCommand(null);
            }
        }

		/// <summary>
		/// Send bytes to device
		/// </summary>
		/// <param name="bytes"></param>
		private void SendBytes(byte[] bytes)
		{
            if (bytes == null)
            {
                return;
            }

            _commandReady = false;  //Block new commands until ready for more
            _commandTimer.Reset(5000);   //Wait maximum 5000 ms for response before sending next command
            Debug.Console(1, this, "Tx: {0}", ComTextHelper.GetEscapedText(bytes));

			if (_commsIsSerial)
				_comms.SendBytes(bytes);
			else
			{
				if (_useHeader)
				{
					// VISCA-over-IP counter
					if (_counter != 0xFFFFFFFF)
						_counter++;
					else
						_counter = 0;

					var header = new byte[]
					{
						0x01, 0x00, 0x00, Convert.ToByte(bytes.Length), (byte)(_counter << 8), (byte)(_counter << 16), (byte)(_counter << 24), (byte)(_counter << 32)
					};

					var cmd = new byte[header.Length + bytes.Length];
					header.CopyTo(cmd, 0);
					bytes.CopyTo(cmd, header.Length);
					_comms.SendBytes(cmd);
				}
				else
					_comms.SendBytes(bytes);
			}
		}

		private void Handle_BytesRecieved(object sender, GenericCommMethodReceiveBytesArgs e)
		{
            try
            {
                _feedbackMutex.WaitForMutex();
                // Append the incoming bytes with whatever is in the buffer
                var newBytes = new byte[_incomingBuffer.Length + e.Bytes.Length];
                _incomingBuffer.CopyTo(newBytes, 0);
                e.Bytes.CopyTo(newBytes, _incomingBuffer.Length);

                // Get data length
                for (int i = 0; i < newBytes.Length; i++)
                {
                    if (newBytes[i] == 0xFF)
                    {
                        int extraDataLength = newBytes.Length - (i + 1);
                        var message = new byte[i + 1];
                        Array.Copy(newBytes, 0, message, 0, i + 1);
                        if (extraDataLength > 0)
                        {
                            // Copy data after FF to new incoming buffer
                            _incomingBuffer = new byte[extraDataLength];
                            Array.Copy(newBytes, i + 1, _incomingBuffer, 0, extraDataLength);
                        }
                        else
                        {
                            _incomingBuffer = new byte[] { };
                        }
                        CrestronInvoke.BeginInvoke((o) => ParseMessage(message));
                        return;
                    }
                }
                if (newBytes.Length <= 16)
                {
                    _incomingBuffer = newBytes;
                }
                else
                {
                    _incomingBuffer = new byte[] { };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning, String.Format("Exception parsing feedback: {0}", ex.Message));
            }
            finally
            {
                _feedbackMutex.ReleaseMutex();
            }
        }

        private void ParseMessage(byte[] message)
        {
            Debug.Console(1, this, "Parsing: {0}, last inquiry: {1}", ComTextHelper.GetEscapedText(message), _lastInquiry.ToString());
            if (message.Length > 2 && message[message.Length - 2] == 0x41 && message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received ack");
                return;
            }
            else if (message.Length > 3 && message[message.Length - 2] == 0x41 && message[message.Length - 3] == 0x61 && message[message.Length - 4] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received command not executable");
                readyForNextCommand(null);
                return;
            }
            else if (message.Length > 2 && message[message.Length - 2] == 0x51 && message[message.Length - 3] == _feedbackAddress)
            {
                Debug.Console(1, this, "Received execution confirmation, last inquiry: {0}", _lastInquiry.ToString());
                switch (_lastInquiry)
                {
                    case eViscaCameraInquiry.PresetSave:
                        ActivePreset = _lastCalledPreset;
                        if (PresetSaved != null)
                        {
                            PresetSaved(this, null);
                        }                            
                        break;
                    case eViscaCameraInquiry.PowerOnCmd:
                        Power = true;
                        CrestronEnvironment.Sleep(2000);
                        PollPower();
                        break;
                    case eViscaCameraInquiry.PowerOffCmd:
                        Power = false;
                        CrestronEnvironment.Sleep(2000);
                        PollPower();
                        break;
                    case eViscaCameraInquiry.AutoTrackOnPresetCmd:
                        AutoTrackingOn = true;
                        break;
                    case eViscaCameraInquiry.AutoTrackOffPresetCmd:
                        AutoTrackingOn = false;
                        break;
                    case eViscaCameraInquiry.AutoTrackOnCmd:
                        CrestronEnvironment.Sleep(2000);
                        PollAutoTrack();
                        break;
                    case eViscaCameraInquiry.AutoTrackOffCmd:
                        CrestronEnvironment.Sleep(2000);
                        PollAutoTrack();
                        break;
                    case eViscaCameraInquiry.PresetRecallCmd:
                        ActivePreset = _lastCalledPreset;
                        break;
                }

                _lastInquiry = eViscaCameraInquiry.NoFeedback;
                readyForNextCommand(null);
                return;
            }
            else if (_lastInquiry != eViscaCameraInquiry.NoFeedback && message.Length > 3)
            {
                switch (_lastInquiry)
                {
                    case eViscaCameraInquiry.PowerInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            if (message[message.Length - 2] == 0x02)
                            {
                                Power = true;
                            }
                            else if (message[message.Length - 2] == 0x03 || message[message.Length - 2] == 0x04)
                            {
                                Power = false;
                            }
                            _lastInquiry = eViscaCameraInquiry.NoFeedback;
                            readyForNextCommand(null);
                        }
                        break;
                    case eViscaCameraInquiry.AutoTrackInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            if (message[message.Length - 2] == 0x02)
                            {
                                AutoTrackingOn = true;
                            }
                            else if (message[message.Length - 2] == 0x03)
                            {
                                AutoTrackingOn = false;
                            }
                            _lastInquiry = eViscaCameraInquiry.NoFeedback;
                            readyForNextCommand(null);
                        }
                        break;
                    case eViscaCameraInquiry.FocusInquiry:
                        if (message[message.Length - 3] == 0x50)
                        {
                            if (message[message.Length - 2] == 0x02)
                            {
                                AutoFocus = true;
                            }
                            else if (message[message.Length - 2] == 0x03)
                            {
                                AutoFocus = false;
                            }
                            _lastInquiry = eViscaCameraInquiry.NoFeedback;
                            readyForNextCommand(null);
                        }
                        break;
                    case eViscaCameraInquiry.PresetInquiry:
                        try
                        {
                            var preset = Convert.ToUInt16(message[message.Length - 2]) + 1;
                            Debug.Console(1, this, "Found preset feedback {0}", preset);
                            if (preset == _config.PrivacyOnPreset)
                            {
                                PrivacyOn = true;
                            }
                            else
                            {
                                PrivacyOn = false;
                            }

                            ActivePreset = (uint)preset;
                        }
                        catch
                        {
                            Debug.Console(0, this, "Exception parsing preset feedback");
                        }
                        _lastInquiry = eViscaCameraInquiry.NoFeedback;
                        readyForNextCommand(null);
                        break;
                    default:
                        _lastInquiry = eViscaCameraInquiry.NoFeedback;
                        Debug.Console(0, this, "Received unknown feedback: {0}", ComTextHelper.GetEscapedText(message));
                        break;
                }
                return;
            }
            else
            {
                Debug.Console(0, this, "Received unknown feedback: {0}", ComTextHelper.GetEscapedText(message));
            }
		}

		/// <summary>
		/// Initialize the camera by sending Address Set Broadcast and IF Clear Broadcasst
		/// </summary>
		public void InitializeCamera()
		{
			// send address set broadcast
			var cmd = new byte[] { 0x88, 0x30, 0x01, 0xFF };
			QueueCommand(cmd);

			// send IF clear on connection
			cmd = new byte[] { 0x88, 0x01, 0x00, 0x01, 0xFF };
			QueueCommand(cmd);
		}

		/// <summary>
		/// Poll 
		/// </summary>
		public void Poll()
		{
            try
            {
                // power inquiry
                PollPower();

                if (!Power)
                {
                    ActivePreset = 0;
                    return;
                }
                if (_autoTrackingCapable && !_usePresetsForAutoTracking)
                {
                    PollAutoTrack();
                }
                PollFocus();
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Exception in poll command: {0}", e.Message);
            }
		}

        public void PollPower()
        {
            var cmd = new byte[] { _address, 0x09, 0x04, 0x00, 0xFF };
            QueueCommand(eViscaCameraInquiry.PowerInquiry, cmd);
        }

        public void PollAutoTrack()
        {
            var cmd = new byte[] { _address, 0x09, 0x0B, 0x00, 0x00, 0xFF };
            QueueCommand(eViscaCameraInquiry.AutoTrackInquiry, cmd);
        }

        public void PollFocus()
        {
            var cmd = new byte[] { _address, 0x09, 0x04, 0x38, 0xFF };
            QueueCommand(eViscaCameraInquiry.FocusInquiry, cmd);
        }

        /// <summary>
        /// Set power state on
        /// </summary>
        public void SetPowerOn()
        {
            QueueCommand(eViscaCameraInquiry.PowerOnCmd, new byte[] { _address, 0x01, 0x04, 0x00, 0x02, 0xFF });
        }

        /// <summary>
        /// Set power state off
        /// </summary>
        public void SetPowerOff()
        {
            QueueCommand(eViscaCameraInquiry.PowerOffCmd, new byte[] { _address, 0x01, 0x04, 0x00, 0x03, 0xFF });
            ActivePreset = 0;
        }

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public void SetAutoTrackingOn()
        {
            if (this._autoTrackingCapable)
            {
                if (_usePresetsForAutoTracking)
                {
                    var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x50, 0xFF };
                    QueueCommand(eViscaCameraInquiry.AutoTrackOnPresetCmd, cmd);
                }
                else
                {
                    var cmd = new byte[] { _address, 0x01, 0x0B, 0x00, 0x00, 0x02, 0xFF };
                    QueueCommand(eViscaCameraInquiry.AutoTrackOnCmd, cmd);
                }                
            }
        }

        /// <summary>
        /// Turn AutoTracking Off
        /// </summary>
        public void SetAutoTrackingOff()
        {
            if (this._autoTrackingCapable)
            {
                if (_usePresetsForAutoTracking)
                {
                    var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x51, 0xFF };
                    QueueCommand(eViscaCameraInquiry.AutoTrackOffPresetCmd, cmd);
                }
                else
                {
                    var cmd = new byte[] { _address, 0x01, 0x0B, 0x00, 0x00, 0x03, 0xFF };
                    QueueCommand(eViscaCameraInquiry.AutoTrackOffCmd, cmd);
                }
            }
        }

        /// <summary>
        /// Move camera with automatic speed setting
        /// </summary>
        /// <param name="state">sig action true/false</param>
        /// <param name="direction">EMoveDirection direction</param>
        public void Move(bool state, EDirection direction)
        {
            if (state && _moveInProgress == EDirection.Stop)
            {
                int count = 0;
                uint slow = 4;
                uint medium = 8;
                uint fast = 12;
                _moveInProgress = direction;

                if (direction == EDirection.ZoomIn || direction == EDirection.ZoomOut)
                {
                    slow = 2;
                    medium = 4;
                    fast = 6;
                }

                Move(direction, slow);

                CrestronInvoke.BeginInvoke((o)=> { 
                    while (_moveInProgress == direction)
                    {
                        if (count == 100)
                        {
                            Move(direction, medium);
                        }
                        else if (count == 200)
                        {
                            Move(direction, fast);
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
                        QueueCommand(new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x01, 0x03, 0xFF });
                        break;
                    }
                case EDirection.PanRight:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x02, 0x03, 0xFF });
                        break;
                    }
                case EDirection.TiltUp:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x03, 0x01, 0xFF });
                        break;
                    }
                case EDirection.TiltDown:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(speed), Convert.ToByte(speed), 0x03, 0x02, 0xFF });
                        break;
                    }
                case EDirection.ZoomIn:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x20 + speed), 0xFF });
                        break;
                    }
                case EDirection.ZoomOut:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x30 + speed), 0xFF });
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
                        QueueCommand(new byte[] { _address, 0x01, 0x06, 0x01, 0x05, 0x05, 0x03, 0x03, 0xFF });
                        break;
                    }
                case EDirection.ZoomIn:
                case EDirection.ZoomOut:
                    {
                        QueueCommand(new byte[] { _address, 0x01, 0x04, 0x07, 0x00, 0xFF });
                        break;
                    }
            }
        }

		/// <summary>
		/// Auto focus on/off
		/// </summary>
		/// <param name="state">sig action true/false</param>
		public void AutoFocusSet(bool state)
		{
            var cmd = state
                ? new byte[] { _address, 0x01, 0x04, 0x38, 0x03, 0xFF }
                : new byte[] { _address, 0x01, 0x04, 0x38, 0x02, 0xFF };
            QueueCommand(cmd);
            PollFocus();
		}

        /// <summary>
        /// Recall Home Position
        /// </summary>
        public void RecallHomePosition()
        {
            _lastCalledPreset = 0;
            QueueCommand(eViscaCameraInquiry.PresetRecallCmd, new byte[] { _address, 0x01, 0x06, 0x04, 0xFF });
        }

		/// <summary>
		/// Recall Preset by Number
		/// </summary>
		/// <param name="preset"></param>
		public void RecallPresetByNumber(uint preset)
		{
			if (preset <= 0)
				return;

            _lastCalledPreset = preset;
			var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(preset-1), 0xFF };
            QueueCommand(eViscaCameraInquiry.PresetRecallCmd, cmd);
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
			var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x01, Convert.ToByte(preset-1), 0xFF };
            QueueCommand(eViscaCameraInquiry.PresetSave, cmd);
		}
	}
}

