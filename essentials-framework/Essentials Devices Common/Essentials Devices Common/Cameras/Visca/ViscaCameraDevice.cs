// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;

namespace ViscaCameraPlugin
{
	public class ViscaCameraDevice : EssentialsBridgeableDevice
	{
		private readonly IBasicCommunication _comms;
		private readonly GenericCommunicationMonitor _commsMonitor;
		private readonly bool _commsIsSerial;
		private readonly bool _useHeader;
		private uint _counter = 0;
        private bool _autoTrackingCapable = false;
        private eViscaCameraInquiry _lastInquiry = eViscaCameraInquiry.None;

		private readonly ViscaCameraConfig _config;

		private readonly byte _address = 0x81;
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
                AutoTrackingOnFeedback.FireUpdate();
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

		/// <summary>
		/// Preset name feedbacks
		/// </summary>
		public Dictionary<uint, StringFeedback> PresetNameFeedbacks { get; private set; }

		private const uint PanSpeedDefault = 9; // 00...18 (hex)
		private const uint PanSpeedMax = 18;
		private uint _panSpeed = PanSpeedDefault;
		/// <summary>
		/// Pan speed feedback
		/// </summary>
		public IntFeedback PanSpeedFeedback { get; private set; }
		/// <summary>
		/// Pan speed
		/// </summary>
		public uint PanSpeed
		{
			get { return (uint)_panSpeed; }
			set
			{
				if (_panSpeed == value) return;
				_panSpeed = (value < 1 || value > PanSpeedMax) ? PanSpeedDefault : value;
				PanSpeedFeedback.FireUpdate();
			}
		}

		private const uint TiltSpeedDefault = 9; // 00...18 (hex)
		private const uint TiltSpeedMax = 18;
		private uint _tiltSpeed = TiltSpeedDefault;
		/// <summary>
		/// Tilt speed feedback
		/// </summary>
		public IntFeedback TiltSpeedFeedback { get; private set; }
		/// <summary>
		/// Tilt speed
		/// </summary>
		public uint TiltSpeed
		{
			get { return (uint)_tiltSpeed; }
			set
			{
				if (_tiltSpeed == value) return;
				_tiltSpeed = (value < 1 || value > TiltSpeedMax) ? TiltSpeedDefault : value;
				TiltSpeedFeedback.FireUpdate();
			}
		}

		private const uint ZoomSpeedDefault = 4; // 00...07 (hex)
		private const uint ZoomSpeedMax = 7;
		private uint _zoomSpeed = ZoomSpeedDefault;
		/// <summary>
		/// Zoom speed feedback
		/// </summary>
		public IntFeedback ZoomSpeedFeedback { get; private set; }
		/// <summary>
		/// Zoom speed
		/// </summary>
		public uint ZoomSpeed
		{
			get { return (uint)_zoomSpeed; }
			set
			{
				if (_zoomSpeed == value) return;
				_zoomSpeed = (value < 1 || value > ZoomSpeedMax) ? ZoomSpeedDefault : value;
				ZoomSpeedFeedback.FireUpdate();
			}
		}

		private const uint FocusSpeedDefault = 4; // 00...07 (hex)
		private const uint FocusSpeedMax = 7;
		private uint _focusSpeed = FocusSpeedDefault;
		/// <summary>
		/// Focus speed feedback
		/// </summary>
		public IntFeedback FocusSpeedFeedback { get; private set; }
		/// <summary>
		/// Focus speed
		/// </summary>
		public uint FocusSpeed
		{
			get { return (uint)_focusSpeed; }
			set
			{
				if (_focusSpeed == value) return;
				_focusSpeed = (value < 1 || value > FocusSpeedMax) ? FocusSpeedDefault : value;
				FocusSpeedFeedback.FireUpdate();
			}
		}
        
        public enum eViscaCameraInquiry
        {
            None,
            Power,
            Focus,
            Preset
        }

		/// <summary>
		/// Move PTZ direction enumeration
		/// </summary>
		public enum EDirection
		{
			Stop = 0,
			Home = 1,
			PanLeft = 2,
			PanRight = 3,
			TiltUp = 4,
			TiltDown = 5,
			ZoomIn = 6,
			ZoomOut = 7,
			FocusAuto = 8,
			FocusNear = 9,
			FocusFar = 10,
			PrivacyOn = 11,
			PrivacyOff = 12
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
			MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);
            AutoTrackingCapable = new BoolFeedback(() => _autoTrackingCapable);

			PowerFeedback = new BoolFeedback(() => Power);
			AutoFocusFeedback = new BoolFeedback(() => AutoFocus);
            AutoTrackingOnFeedback = new BoolFeedback(() => AutoTrackingOn);
			PanSpeedFeedback = new IntFeedback(() => (int)PanSpeed);
			TiltSpeedFeedback = new IntFeedback(() => (int)TiltSpeed);
			ZoomSpeedFeedback = new IntFeedback(() => (int)ZoomSpeed);
			FocusSpeedFeedback = new IntFeedback(() => (int)FocusSpeed);
			PresetCountFeedback = new IntFeedback(() => (int)PresetCount);
			PresetNameFeedbacks = new Dictionary<uint, StringFeedback>();

            if (_config.AutoTracking == true)
                _autoTrackingCapable = true;

			if (_config.PollTimeMs > 0 && _config.PollTimeMs != _pollTimeMs)
				_pollTimeMs = _config.PollTimeMs;

			if (_config.WarningTimeoutMs > 0 && _config.WarningTimeoutMs != _warningTimeoutMs)
				_warningTimeoutMs = _config.WarningTimeoutMs;

			if (_config.ErrorTimeoutMs > 0 && _config.ErrorTimeoutMs != _errorTimeoutMs)
				_errorTimeoutMs = _config.ErrorTimeoutMs;

			if (_config.Address > 0 && _config.Address <= AddressMax && _config.Address != _address)
				_address = Convert.ToByte(0x80 + _config.Address);

			if (_config.PanSpeed > 0 && _config.PanSpeed <= PanSpeedMax && _config.PanSpeed != PanSpeed)
				PanSpeed = _config.PanSpeed;

			if (_config.TiltSpeed > 0 && _config.TiltSpeed <= TiltSpeedMax && _config.TiltSpeed != TiltSpeed)
				TiltSpeed = _config.TiltSpeed;

			if (_config.ZoomSpeed > 0 && _config.ZoomSpeed <= ZoomSpeedMax && _config.ZoomSpeed != ZoomSpeed)
				ZoomSpeed = _config.ZoomSpeed;

			if (_config.FocusSpeed > 0 && _config.FocusSpeed <= FocusSpeedMax && _config.FocusSpeed != FocusSpeed)
				FocusSpeed = _config.FocusSpeed;

			if (_config.PrivacyOnPreset > 0 && _config.PrivacyOnPreset <= PresetMax)
				_privacyOnPreset = _config.PrivacyOnPreset;

			if (_config.PrivacyOffPreset > 0 && _config.PrivacyOffPreset <= PresetMax)
				_privacyOffPreset = config.PrivacyOffPreset;

			if (_config.Control.Method.ToString().ToLower() == "udp")
				_useHeader = true;

			_comms = comms;
			_comms.BytesReceived += Handle_BytesRecieved;
			_commsMonitor = new GenericCommunicationMonitor(this, _comms, _pollTimeMs, _warningTimeoutMs, _errorTimeoutMs, Poll);

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
				_commsMonitor.Start();
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
			_commsMonitor.Start();

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

                if (PresetNameFeedbacks.ContainsKey(index.Index))
                    PresetNameFeedbacks[index.Index] = new StringFeedback(() => index.Name);
				else
                    PresetNameFeedbacks.Add(index.Index, new StringFeedback(() => index.Name));
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

            _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			// must null check so LinkToApi doesn't except when the device is TCP or UDP
			if (SocketStatusFeedback != null)
				SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
			//MonitorStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);

			// power on
			trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, () => SetPower(true));
			PowerFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
			// power off
			trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, () => SetPower(false));
			PowerFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

			// home
			trilist.SetBoolSigAction(joinMap.Home.JoinNumber, sig => Move(sig, EDirection.Home));

			// pan
			trilist.SetBoolSigAction(joinMap.PanLeft.JoinNumber, sig => Move(sig, EDirection.PanLeft));
			trilist.SetBoolSigAction(joinMap.PanRight.JoinNumber, sig => Move(sig, EDirection.PanRight));
			trilist.SetUShortSigAction(joinMap.PanSpeed.JoinNumber, value => SetPanSpeed(value));
			PanSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.PanSpeed.JoinNumber]);

			// tilt
			trilist.SetBoolSigAction(joinMap.TiltUp.JoinNumber, sig => Move(sig, EDirection.TiltUp));
			trilist.SetBoolSigAction(joinMap.TiltDown.JoinNumber, sig => Move(sig, EDirection.TiltDown));
			trilist.SetUShortSigAction(joinMap.TiltSpeed.JoinNumber, value => SetTiltSpeed(value));
			TiltSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.TiltSpeed.JoinNumber]);

			// zoom
			trilist.SetBoolSigAction(joinMap.ZoomIn.JoinNumber, sig => Move(sig, EDirection.ZoomIn));
			trilist.SetBoolSigAction(joinMap.ZoomOut.JoinNumber, sig => Move(sig, EDirection.ZoomOut));
			trilist.SetUShortSigAction(joinMap.ZoomSpeed.JoinNumber, value => SetZoomSpeed(value));
			ZoomSpeedFeedback.LinkInputSig(trilist.UShortInput[joinMap.ZoomSpeed.JoinNumber]);

            // auto tracking on
            trilist.SetSigTrueAction(joinMap.AutoTrackingOn.JoinNumber, SetAutoTrackingOn);
            AutoTrackingOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoTrackingOn.JoinNumber]);

            // auto tracking off
            trilist.SetSigTrueAction(joinMap.AutoTrackingOff.JoinNumber, SetAutoTrackingOff);
            AutoTrackingOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoTrackingOff.JoinNumber]);

			// focus
			trilist.SetBoolSigAction(joinMap.AutoFocus.JoinNumber, sig => Move(sig, EDirection.FocusAuto));
			AutoFocusFeedback.LinkInputSig(trilist.BooleanInput[joinMap.AutoFocus.JoinNumber]);

			// privacy
			trilist.SetBoolSigAction(joinMap.PrivacyOn.JoinNumber, sig => Move(sig, EDirection.PrivacyOn));
			trilist.SetBoolSigAction(joinMap.PrivacyOff.JoinNumber, sig => Move(sig, EDirection.PrivacyOff));
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
			// must null check so LinkToApi doesn't except when the device is TCP or UDP
			if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();
			//MonitorStatusFeedback.FireUpdate();

			PowerFeedback.FireUpdate();
			PanSpeedFeedback.FireUpdate();
			TiltSpeedFeedback.FireUpdate();
			ZoomSpeedFeedback.FireUpdate();
			PresetCountFeedback.FireUpdate();
            AutoTrackingCapable.FireUpdate();
            AutoTrackingOnFeedback.FireUpdate();

			foreach (var item in PresetNameFeedbacks)
				item.Value.FireUpdate();
		}

		#endregion

		private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
			Debug.Console(1, this, args.Client.ClientStatus.ToString());

			OnlineFeedback.FireUpdate();
			// must null check so LinkToApi doesn't except when the device is TCP or UDP
			if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();

			if (args.Client.IsConnected) InitializeCamera();
		}


		/// <summary>
		/// Send bytes to device
		/// </summary>
		/// <param name="bytes"></param>
		public void SendBytes(byte[] bytes)
		{
			if (bytes == null) return;

			if (_commsIsSerial)
				_comms.SendBytes(bytes);
			else
			{
				if (!_comms.IsConnected)
					_comms.Connect();

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

		private void Handle_BytesRecieved(object sender, GenericCommMethodReceiveBytesArgs args)
		{
			if (args == null || args.Bytes == null)
			{
				Debug.Console(2, this, "Handle_BytesRecieved args or args.Bytes is null");
				return;
			}

			Debug.Console(2, this, "Handle_BytesRecieved: {0}", args.Bytes);

            if (_lastInquiry != eViscaCameraInquiry.None)
            {
                switch (_lastInquiry)
                {
                    case eViscaCameraInquiry.Power:
                        if (args.Bytes.Length > 3 && args.Bytes[1] == 0x50  && args.Bytes[3] == 0xFF)
                        {
                            if(args.Bytes[2] == 02)
                            {
                                Power = true;
                            }
                            else if(args.Bytes[2] == 03 || args.Bytes[2] == 04)
                            {
                                Power = false;
                            }
                        }
                        break;
                    case eViscaCameraInquiry.Focus:
                        if (args.Bytes.Length > 3 && args.Bytes[1] == 0x50 && args.Bytes[3] == 0xFF)
                        {
                            if (args.Bytes[2] == 02)
                            {
                                AutoFocus = true;
                            }
                            else if (args.Bytes[2] == 03)
                            {
                                AutoFocus = false;
                            }
                        }
                        break;
                    case eViscaCameraInquiry.Preset:
                        if (args.Bytes.Length > 3 && args.Bytes[1] == 0x50 && args.Bytes[3] == 0xFF)
                        {
                            var preset = Convert.ToUInt16(args.Bytes[2]);
                            if (preset == 80)
                            {
                                AutoTrackingOn = true;
                            }
                            else
                            {
                                AutoTrackingOn = false;
                            }

                            if (preset == _config.PrivacyOnPreset)
                            {
                                //Set privacy feedback on
                            }
                            else
                            {
                                //Set privacy feedback off
                            }

                            //Todo - set preset feedback
                        }
                        break;
                }
            }
		}

		/// <summary>
		/// Initialize the camera by sending Address Set Broadcast and IF Clear Broadcasst
		/// </summary>
		public void InitializeCamera()
		{
			// send address set broadcast
			var cmd = new byte[] { 0x88, 0x30, 0x01, 0xFF };
			SendBytes(cmd);

			// send IF clear on connection
			cmd = new byte[] { 0x88, 0x01, 0x00, 0x01, 0xFF };
			SendBytes(cmd);
		}

		/// <summary>
		/// Poll 
		/// </summary>
		public void Poll()
		{
			// power inquiry
			var cmd = new byte[] { _address, 0x09, 0x04, 0x00, 0xFF };
			SendBytes(cmd);

			if (!Power) return;

			// focus mode inquiry
			cmd = new byte[] { _address, 0x09, 0x04, 0x38, 0xFF };
			SendBytes(cmd);
		}

		/// <summary>
		/// Set power state
		/// </summary>
		/// <param name="state">power on/off</param>
		public void SetPower(bool state)
		{
			// Power ? [send off] : [send on]
			var cmd = Power
				? new byte[] { _address, 0x01, 0x04, 0x00, 0x03, 0xFF }
				: new byte[] { _address, 0x01, 0x04, 0x00, 0x02, 0xFF };

			SendBytes(cmd);
		}

        /// <summary>
        /// Turn AutoTracking On
        /// </summary>
        public void SetAutoTrackingOn()
        {
            var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x50, 0xFF };
            SendBytes(cmd);
        }

        /// <summary>
        /// Turn AutoTracking Off
        /// </summary>
        public void SetAutoTrackingOff()
        {
            var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, 0x51, 0xFF };
            SendBytes(cmd);
        }

		/// <summary>
		/// Move camera
		/// </summary>
		/// <param name="state">sig action true/false</param>
		/// <param name="direction">EMoveDirection direction</param>
		public void Move(bool state, EDirection direction)
		{
			switch (direction)
			{
				case EDirection.Home:
					{
						var cmd = state
							? new byte[] { _address, 0x01, 0x06, 0x04, 0xFF }
							: null;
						SendBytes(cmd);
						break;
					}
				case EDirection.PanLeft:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x01, 0x03, 0xFF }
							: new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x03, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.PanRight:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x02, 0x03, 0xFF }
							: new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x03, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.TiltUp:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x01, 0xFF }
							: new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x03, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.TiltDown:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x02, 0xFF }
							: new byte[] { _address, 0x01, 0x06, 0x01, Convert.ToByte(PanSpeed), Convert.ToByte(TiltSpeed), 0x03, 0x03, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.ZoomIn:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x20 + ZoomSpeed), 0xFF }
							: new byte[] { _address, 0x01, 0x04, 0x07, 0x00, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.ZoomOut:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x04, 0x07, Convert.ToByte(0x30 + ZoomSpeed), 0xFF }
							: new byte[] { _address, 0x01, 0x04, 0x07, 0x00, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.FocusAuto:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x04, 0x38, 0x03, 0xFF }
							: new byte[] { _address, 0x01, 0x04, 0x38, 0x02, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.FocusNear:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x04, 0x08, Convert.ToByte(0x30 + FocusSpeed), 0xFF }
							: new byte[] { _address, 0x01, 0x04, 0x08, 0x02, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.FocusFar:
					{
						// state ? [moving] : [stop]
						var cmd = state
							? new byte[] { _address, 0x01, 0x04, 0x08, Convert.ToByte(0x20 + FocusSpeed), 0xFF }
							: new byte[] { _address, 0x01, 0x04, 0x08, 0x02, 0xFF };
						SendBytes(cmd);
						break;
					}
				case EDirection.PrivacyOn:
					{
						if (_privacyOnPreset == 0) return;
						RecallPresetByNumber(_privacyOnPreset);
						break;
					}
				case EDirection.PrivacyOff:
					{
						if (_privacyOffPreset == 0) return;
						RecallPresetByNumber(_privacyOffPreset);
						break;
					}
			}
		}

		/// <summary>
		/// Recall Preset by Number
		/// </summary>
		/// <param name="preset"></param>
		public void RecallPresetByNumber(uint preset)
		{
			if (preset <= 0)
				return;

			var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x02, Convert.ToByte(preset), 0xFF };
			SendBytes(cmd);
		}

		/// <summary>
		/// Save Preset by Number
		/// </summary>
		/// <param name="preset">preset 1...16</param>
		public void SavePresetByNumber(uint preset)
		{
			if (preset <= 0)
				return;

			var cmd = new byte[] { _address, 0x01, 0x04, 0x3F, 0x01, Convert.ToByte(preset), 0xFF };
			SendBytes(cmd);
            if (PresetSaved != null)
            {
                PresetSaved(this, null);
            }
		}

		/// <summary>
		/// Sets the pan speed of the camera
		/// </summary>
		/// <param name="value">00...18(hex)</param>
		public void SetPanSpeed(uint value)
		{
			PanSpeed = value > PanSpeedMax ? PanSpeedDefault : value;
		}

		/// <summary>
		/// Sets the tilt speed of the camera
		/// </summary>
		/// <param name="value">00...18 (hex)</param>
		public void SetTiltSpeed(uint value)
		{
			TiltSpeed = value > TiltSpeedMax ? TiltSpeedDefault : value;
		}

		/// <summary>
		/// Sets the zoom speed of the camera
		/// </summary>
		/// <param name="value">00...07 (hex)</param>
		public void SetZoomSpeed(uint value)
		{
			ZoomSpeed = value > ZoomSpeedMax ? ZoomSpeedDefault : value;
		}

		/// <summary>
		/// Sets the focus speed of the camera
		/// </summary>
		/// <param name="value">00...07 (hex)</param>
		public void SetFocusSpeed(uint value)
		{
			FocusSpeed = value > FocusSpeedMax ? FocusSpeedDefault : value;
		}
	}
}

