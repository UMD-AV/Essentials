using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Core;
using Crestron.SimplSharp;

namespace VaddioBridgePlugin
{
	public class VaddioBridgeDevice : EssentialsBridgeableDevice
	{
		private readonly IBasicCommunication _comms;
		private byte[] _commsByteBuffer = new byte[] { };
		private readonly GenericCommunicationMonitor _commsMonitor;
        private readonly bool _commsIsSerial;
        private bool _loggedIn;
        private bool _usernameSent;
        private CMutex _bufferMutex;

		private readonly VaddioBridgeConfig _config;

		private readonly long _pollTimeMs = 30000; // 30s
		private readonly long _warningTimeoutMs = 60000; // 60s
		private readonly long _errorTimeoutMs = 180000; // 180s

        private readonly string usernameSearch = "login:";
        private readonly string passwordSearch = "password:";
        private readonly string pipSearch = "pip:";
        private readonly string layoutSearch = "layout:";
        private readonly string sourceSearch = "source:";
        private readonly string standbySearch = "standby:";
        private readonly string ipAddressSearch = "ip address";
        private readonly string versionSearch = "system version";
        private readonly string syntaxErrorSearch = "syntax error";
        private readonly string incorrectPasswordSearch = "login incorrect";

		private bool _powerOn;
		/// <summary>
		/// Power feedback
		/// </summary>
		public BoolFeedback PowerOnFeedback { get; private set; }
		/// <summary>
		/// Power property
		/// </summary>
		public bool PowerOn
		{
			get { return _powerOn; }
			set
			{
				if (_powerOn == value) return;
				_powerOn = value;
				PowerOnFeedback.FireUpdate();
			}
		}

        private bool _pipOn;
        /// <summary>
        /// Pip feedback
        /// </summary>
        public BoolFeedback PipOnFeedback { get; private set; }
        /// <summary>
        /// Pip property
        /// </summary>
        public bool PipOn
        {
            get { return _pipOn; }
            set
            {
                if (_pipOn == value) return;
                _pipOn = value;
                PipOnFeedback.FireUpdate();
            }
        }

        private ePipLayout _pipLayout;
        /// <summary>
        /// Pip layout feedback
        /// </summary>
        public BoolFeedback PipUpperLeftFeedback { get; private set; }
        public BoolFeedback PipUpperRightFeedback { get; private set; }
        public BoolFeedback PipLowerLeftFeedback { get; private set; }
        public BoolFeedback PipLowerRightFeedback { get; private set; }
        public BoolFeedback PipTopBottomFeedback { get; private set; }
        public BoolFeedback PipLeftRightFeedback { get; private set; }
        /// <summary>
        /// Pip layout property
        /// </summary>
        public ePipLayout PipLayout
        {
            get { return _pipLayout; }
            set
            {
                if (_pipLayout == value) return;
                _pipLayout = value;
                PipUpperLeftFeedback.FireUpdate();
                PipUpperRightFeedback.FireUpdate();
                PipLowerLeftFeedback.FireUpdate();
                PipLowerRightFeedback.FireUpdate();
                PipTopBottomFeedback.FireUpdate();
                PipLeftRightFeedback.FireUpdate();
            }
        }

        private string _ipAddress;
        /// <summary>
        /// Ip Address feedback
        /// </summary>
        public StringFeedback IpAddressFeedback { get; private set; }
        /// <summary>
        /// Ip Address property
        /// </summary>
        public string IpAddress
        {
            get { return _ipAddress; }
            set
            {
                if (_ipAddress == value) return;
                _ipAddress = value;
                IpAddressFeedback.FireUpdate();
            }
        }

        private uint _source;
        /// <summary>
        /// Ip Address feedback
        /// </summary>
        public IntFeedback SourceFeedback { get; private set; }
        /// <summary>
        /// Ip Address property
        /// </summary>
        public uint Source
        {
            get { return _source; }
            set
            {
                if (_source == value) return;
                _source = value;
                SourceFeedback.FireUpdate();
            }
        }

        private string _firmwareVersion;
        /// <summary>
        /// Firmware feedback
        /// </summary>
        public StringFeedback FirmwareVersionFeedback { get; private set; }
        /// <summary>
        /// Firmware property
        /// </summary>
        public string FirmwareVersion
        {
            get { return _firmwareVersion; }
            set
            {
                if (_firmwareVersion == value) return;
                _firmwareVersion = value;
                FirmwareVersionFeedback.FireUpdate();
            }
        }

		/// <summary>
		/// Pip Layout enumeration
		/// </summary>
		public enum ePipLayout
		{
			UpperLeft = 0,
			UpperRight = 1,
			LowerLeft = 2,
			LowerRight = 3,
			TopBottom = 4,
			LeftRight = 5
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
		/// Constructor
		/// </summary>
		/// <param name="key">device key</param>
		/// <param name="name">device name</param>
		/// <param name="config">device config</param>
		/// <param name="comms">IBasicCommunications</param>
		public VaddioBridgeDevice(string key, string name, IBasicCommunication comms, VaddioBridgeConfig config)
			: base(key, name)
		{
			Debug.Console(0, this, "Constructing new Vaddio Bridge instance");

			_config = config;
            _loggedIn = false;
            _usernameSent = false;

            _bufferMutex = new CMutex();

			OnlineFeedback = new BoolFeedback(() => _comms.IsConnected);
			MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);

			PowerOnFeedback = new BoolFeedback(() => PowerOn);
            PipOnFeedback = new BoolFeedback(() => PipOn);
            PipUpperLeftFeedback = new BoolFeedback(() => PipLayout == ePipLayout.UpperLeft);
            PipUpperRightFeedback = new BoolFeedback(() => PipLayout == ePipLayout.UpperRight);
            PipLowerLeftFeedback = new BoolFeedback(() => PipLayout == ePipLayout.LowerLeft);
            PipLowerRightFeedback = new BoolFeedback(() => PipLayout == ePipLayout.LowerRight);
            PipTopBottomFeedback = new BoolFeedback(() => PipLayout == ePipLayout.TopBottom);
            PipLeftRightFeedback = new BoolFeedback(() => PipLayout == ePipLayout.LeftRight);

            SourceFeedback = new IntFeedback(() => (int)Source);
			IpAddressFeedback = new StringFeedback(() => IpAddress);
            FirmwareVersionFeedback = new StringFeedback(() => FirmwareVersion);

			if (_config.PollTimeMs > 0 && _config.PollTimeMs != _pollTimeMs)
				_pollTimeMs = _config.PollTimeMs;

			if (_config.WarningTimeoutMs > 0 && _config.WarningTimeoutMs != _warningTimeoutMs)
				_warningTimeoutMs = _config.WarningTimeoutMs;

			if (_config.ErrorTimeoutMs > 0 && _config.ErrorTimeoutMs != _errorTimeoutMs)
				_errorTimeoutMs = _config.ErrorTimeoutMs;

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
			}
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
			var joinMap = new VaddioBridgeJoinMap(joinStart);

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

            _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            IpAddressFeedback.LinkInputSig(trilist.StringInput[joinMap.IpAddress.JoinNumber]);
            FirmwareVersionFeedback.LinkInputSig(trilist.StringInput[joinMap.FirmwareVersion.JoinNumber]);

			// power on
			trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, SetPowerOn);
			PowerOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
			// power off
			trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, SetPowerOff);
			PowerOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

            // pip on
            trilist.SetSigTrueAction(joinMap.PipOn.JoinNumber, SetPipOn);
            PipOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipOn.JoinNumber]);
            // pip off
            trilist.SetSigTrueAction(joinMap.PipOff.JoinNumber, SetPipOff);
            PipOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PipOff.JoinNumber]);

			// pip layout
            trilist.SetSigTrueAction(joinMap.PipToggle.JoinNumber, TogglePipLayout);

            trilist.SetSigTrueAction(joinMap.PipUpperLeft.JoinNumber, () => SetPipLayout(ePipLayout.UpperLeft));
			PipUpperLeftFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipUpperLeft.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PipUpperRight.JoinNumber, () => SetPipLayout(ePipLayout.UpperRight));
            PipUpperRightFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipUpperRight.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PipLowerLeft.JoinNumber, () => SetPipLayout(ePipLayout.LowerLeft));
            PipLowerLeftFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipLowerLeft.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PipLowerRight.JoinNumber, () => SetPipLayout(ePipLayout.LowerRight));
            PipLowerRightFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipLowerRight.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PipTopBottom.JoinNumber, () => SetPipLayout(ePipLayout.TopBottom));
            PipTopBottomFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipTopBottom.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.PipLeftRight.JoinNumber, () => SetPipLayout(ePipLayout.LeftRight));
            PipLeftRightFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PipLeftRight.JoinNumber]);

            //video source
            trilist.SetUShortSigAction(joinMap.VideoSource.JoinNumber, sig => SetVideoSource(sig));
            SourceFeedback.LinkInputSig(trilist.UShortInput[joinMap.VideoSource.JoinNumber]);
            
			UpdateFeedbacks();

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
            IpAddressFeedback.FireUpdate();
            FirmwareVersionFeedback.FireUpdate();
			PowerOnFeedback.FireUpdate();
			PipOnFeedback.FireUpdate();
            PipUpperLeftFeedback.FireUpdate();
            PipUpperRightFeedback.FireUpdate();
            PipLowerLeftFeedback.FireUpdate();
            PipLowerRightFeedback.FireUpdate();
            PipTopBottomFeedback.FireUpdate();
            PipLeftRightFeedback.FireUpdate();
            SourceFeedback.FireUpdate();

            if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();
		}

		#endregion

		private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
			Debug.Console(1, this, args.Client.ClientStatus.ToString());

			OnlineFeedback.FireUpdate();
            if (SocketStatusFeedback != null) SocketStatusFeedback.FireUpdate();

			if (args.Client.IsConnected) Poll();
		}


		/// <summary>
		/// Send bytes to device
		/// </summary>
		/// <param name="bytes"></param>
		public void SendText(string text)
		{
			if (text == null) return;

            text += "\x0D";
            Debug.Console(1, this, "Sending text: {0}", text);

			if (_commsIsSerial)
				_comms.SendText(text);
			else
			{
				if (!_comms.IsConnected)
					_comms.Connect();

                _comms.SendText(text);
			}
		}

		private void Handle_BytesRecieved(object sender, GenericCommMethodReceiveBytesArgs args)
        {
            bool gotMutex = _bufferMutex.WaitForMutex();
            if (gotMutex)
            {
                try
                {
                    if (args == null || args.Bytes == null)
                    {
                        Debug.Console(1, this, "Handle_BytesRecieved args is null or args.Bytes is null");
                        return;
                    }

                    var byteBuffer = new byte[_commsByteBuffer.Length + args.Bytes.Length];
                    _commsByteBuffer.CopyTo(byteBuffer, 0);
                    args.Bytes.CopyTo(byteBuffer, _commsByteBuffer.Length);

                    Debug.Console(2, this, "Handle_BytesRecieved byteBuffer: {0}", ComTextHelper.GetDebugText(Encoding.UTF8.GetString(byteBuffer, 0, byteBuffer.Length)));

                    int position = 0;

                    for (int i = 0; i < byteBuffer.Length; i++)
                    {
                        if (byteBuffer[i] == 0x0A)
                        {
                            //Found new line                      
                            if (i > 0)
                            {
                                byte[] gatheredBytes = byteBuffer.Skip(position).Take(i-position).ToArray();
                                string gatheredText = Encoding.UTF8.GetString(gatheredBytes, 0, gatheredBytes.Length);
                                Debug.Console(1, this, "Gathered text: {0}", gatheredText);
                                processFeedback(gatheredText);
                            }

                            position = i + 1;
                        }

                        else if (byteBuffer[i] == 0x3E)  // 0x3E = ">"
                        {
                            // Found ">"
                            position = i + 1;
                        }
                    }

                    // save partial message here
                    _commsByteBuffer = byteBuffer.Skip(position).ToArray();
                }
                catch(Exception ex)
                {
                    Debug.Console(0, this, "Handle_BytesRecieved exception: {0}", ex);
                }
                finally
                {
                    _bufferMutex.ReleaseMutex();
                }
            }
        }

        private void processFeedback(string feedback)
        {
            string temp = feedback.ToLower().Trim();
            if (temp.StartsWith("ok"))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: ok");
                if (_loggedIn == false)
                {
                    _loggedIn = true;
                    CrestronInvoke.BeginInvoke((o) => InitializeBridge());
                }
            }
            else if (temp.StartsWith(incorrectPasswordSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: login incorrect");
                _loggedIn = false;
                if (_usernameSent == false)
                {
                    _usernameSent = true;
                    string username = _config.Username == null ? "admin" : _config.Username;
                    SendText(username);
                }
            }
            else if (temp.Contains(usernameSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: login");
                _loggedIn = false;
                if (_usernameSent == false)
                {
                    _usernameSent = true;
                    string username = _config.Username == null ? "admin" : _config.Username;
                    SendText(username);                   
                }
            }
            else if (temp.Contains(passwordSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: password");
                if (_usernameSent == true)
                {
                    string password = _config.Password == null ? "" : _config.Password;
                    SendText(password);
                }
                _usernameSent = false;
            }
            else if (temp.StartsWith(pipSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: pip");
                string pip = temp.Substring(pipSearch.Length, temp.Length - pipSearch.Length).Trim();
                PipOn = pip == "on";
            }
            else if (temp.StartsWith(layoutSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: layout");
                string layout = temp.Substring(layoutSearch.Length, temp.Length - layoutSearch.Length).Trim();
                switch (layout)
                {
                    case "upper_left":
                        PipLayout = ePipLayout.UpperLeft;
                        break;
                    case "upper_right":
                        PipLayout = ePipLayout.UpperRight;
                        break;
                    case "lower_left":
                        PipLayout = ePipLayout.LowerLeft;
                        break;
                    case "lower_right":
                        PipLayout = ePipLayout.LowerRight;
                        break;
                    case "top_bottom":
                        PipLayout = ePipLayout.TopBottom;
                        break;
                    case "left_right":
                        PipLayout = ePipLayout.LeftRight;
                        break;
                }
            }
            else if (temp.StartsWith(sourceSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: source");
                string source = temp.Substring(sourceSearch.Length, temp.Length - sourceSearch.Length).Trim();
                switch (source)
                {
                    case "input1":
                        Source = 1;
                        break;
                    case "input2":
                        Source = 2;
                        break;
                }
            }
            else if (temp.StartsWith(standbySearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: standby");
                string standby = temp.Substring(standbySearch.Length, temp.Length - standbySearch.Length).Trim();
                PowerOn = standby == "off";
            }
            else if (temp.StartsWith(ipAddressSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: ip address");
                IpAddress = temp.Substring(ipAddressSearch.Length, temp.Length - ipAddressSearch.Length).Trim();
            }
            else if (temp.StartsWith(versionSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: version");
                FirmwareVersion = temp.Substring(versionSearch.Length, temp.Length - versionSearch.Length).Trim();
            }
            else if (temp.StartsWith(syntaxErrorSearch))
            {
                Debug.Console(1, this, "Vaddio Bridge feedback: syntax error");
                _loggedIn = true;
            }
        }

		/// <summary>
		/// Initialize the bridge
		/// </summary>
		public void InitializeBridge()
		{
            Poll();
            PollVersion();
            PollPower();
            PollPip();
            PollPipLayout();
            PollSource();
		}

		/// <summary>
		/// Poll 
		/// </summary>
		public void Poll()
		{
            if (_loggedIn == false)
            {
                if (_usernameSent == false)
                {
                    _usernameSent = true;
                    string username = _config.Username == null ? "admin" : _config.Username;
                    SendText(username);
                }
                else
                {
                    string password = _config.Password == null ? "" : _config.Password;
                    SendText(password);
                    _usernameSent = false;
                }
            }
            else
            {
                SendText("network settings get");
            }
		}

        /// <summary>
        /// Poll Firmware Version
        /// </summary>
        public void PollVersion()
        {
            SendText("version");
        }

        /// <summary>
        /// Poll Power State
        /// </summary>
        public void PollPower()
        {
            SendText("system standby get");
        }

        /// <summary>
        /// Poll Pip State
        /// </summary>
        public void PollPip()
        {
            SendText("video program pip get");
        }

        /// <summary>
        /// Poll Pip State
        /// </summary>
        public void PollPipLayout()
        {
            SendText("video program pip layout get");
        }

        /// <summary>
        /// Poll Source State
        /// </summary>
        public void PollSource()
        {
            SendText("video program source get");
        }

		/// <summary>
		/// Set power on
		/// </summary>
		public void SetPowerOn()
		{
            SendText("system standby off");
            PollPower();
		}

        /// <summary>
        /// Set power off
        /// </summary>
        public void SetPowerOff()
        {
            SendText("system standby on");
            PollPower();
        }

        /// <summary>
        /// Set pip on
        /// </summary>
        public void SetPipOn()
        {
            SendText("video program pip on");
            PollPip();
        }

        /// <summary>
        /// Set pip off
        /// </summary>
        public void SetPipOff()
        {
            SendText("video program pip off");
            PollPip();
        }

        /// <summary>
        /// Toggle pip layout
        /// </summary>
        public void TogglePipLayout()
        {
            SendText("video program pip toggle");
            PollPipLayout();
        }

        /// <summary>
        /// Set pip layout state
        /// </summary>
        /// <param name="layout">ePipLayout</param>
        public void SetPipLayout(ePipLayout layout)
        {
            switch (layout)
            {
                case ePipLayout.UpperLeft:
                    SendText("video program pip layout upper_left");
                    break;
                case ePipLayout.UpperRight:
                    SendText("video program pip layout upper_right");
                    break;
                case ePipLayout.LowerLeft:
                    SendText("video program pip layout lower_left");
                    break;
                case ePipLayout.LowerRight:
                    SendText("video program pip layout lower_right");
                    break;
                case ePipLayout.TopBottom:
                    SendText("video program pip layout top_bottom");
                    break;
                case ePipLayout.LeftRight:
                    SendText("video program pip layout left_right");
                    break;
            }
            PollPipLayout();
        }

        /// <summary>
        /// Set program source
        /// </summary>
        /// <param name="source">source number</param>
        public void SetVideoSource(uint source)
        {
            if (source > 0)
            {
                SendText(string.Format("video program source set input{0}", source));
                PollSource();
            }
        }
	}
}

