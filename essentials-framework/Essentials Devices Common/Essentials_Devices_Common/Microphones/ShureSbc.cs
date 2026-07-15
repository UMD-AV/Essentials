using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;
using UmdEssentials.Core.Queues;

namespace UmdEssentials.Devices.Common.Microphones
{
    public class ShureSbcDevice : EssentialsBridgeableDevice, IHasErrorString
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";
        private readonly GenericQueue _commsQueue;
        public int SbcSize { get; private set; }
        public readonly ShureSbcBattery[] Microphones;

        private readonly Regex _regexPattern = new Regex(
            @"< REP (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >",
            RegexOptions.IgnoreCase);

        private readonly CommunicationGather _commsGather;

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        /// Reports monitor status feedback through the bridge
        /// Typically used for Fusion status reporting and system status LED's
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }

        #region Device Info

        // device model field
        private string _deviceModel;

        /// <summary>
        /// Device model property
        /// </summary>
        public string DeviceModel
        {
            get { return _deviceModel; }
            set
            {
                _deviceModel = value;
                DeviceModelFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Device model feedback
        /// </summary>
        public StringFeedback DeviceModelFeedback { get; private set; }

        // device firmware version field
        private string _deviceFirmwareVersion;

        /// <summary>
        /// Device firmware property
        /// </summary>
        public string DeviceFirmwareVersion
        {
            get { return _deviceFirmwareVersion; }
            set
            {
                _deviceFirmwareVersion = value;
                DeviceFirmwareVersionFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Device firmware version feedback
        /// </summary>
        public StringFeedback DeviceFirmwareVersionFeedback { get; private set; }

        // device error field
        private string _deviceError;

        /// <summary>
        /// Device error property
        /// </summary>
        public string DeviceError
        {
            get { return _deviceError; }
            set
            {
                _deviceError = value;
                ErrorFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Device error feedback
        /// </summary>
        public StringFeedback ErrorFeedback { get; private set; }

        #endregion

        /// <summary>
        /// Device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="config">device configuration object</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication"/>
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        public ShureSbcDevice(string key, string name, MicController config, IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            MonitorStatusFeedback = new IntFeedback(() =>
            {
                if (_commsMonitor != null) return (int)_commsMonitor.Status;
                return 0;
            });
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            ErrorFeedback = new StringFeedback(() => DeviceError);

            SbcSize = 8;
            Microphones = new ShureSbcBattery[SbcSize];
            ushort i = 0;
            while (i < config.MicKeys.Length)
            {
                Microphones[i] = new ShureSbcBattery(config.MicKeys[i], string.Format("{0} Slot {1}", name, i + 1))
                {
                    Model = "Shure Battery",
                    IsOnline = true
                };
                try
                {
                    DeviceManager.AddDevice(Microphones[i]);
                }
                catch (Exception e)
                {
                    Debug.ConsoleWithLog(0, this, "Exception adding mic '{0}' to device manager: {1}",
                        config.MicKeys[i],
                        e.Message);
                }

                i++;
            }

            while (i < SbcSize)
            {
                Microphones[i] =
                    new ShureSbcBattery(Key + "-battery" + i + 1, string.Format("{0} Slot {1}", name, i + 1))
                    {
                        Model = "Shure Battery",
                        IsOnline = true
                    };
                i++;
            }

            _comms = comms;
            _commsGather = new CommunicationGather(_comms, CommsDelimiter)
                { IncludeDelimiter = true };
            _commsGather.LineReceived += Handle_LineReceived;
            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _commsQueue = new GenericQueue(key + "-queue");

            ISocketStatus socket = _comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is Ethernet, otherwise device comms is RS232
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }
        }

        /// <summary>
        /// Use the custom activate method to connect the device and start the comms monitor.
        /// This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            _comms.Connect();
            _commsMonitor.Start();

            return base.CustomActivate();
        }

        // socket connection change event handler
        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.FireUpdate();

            if (args.Client.IsConnected)
                UpdateStatus();
        }

        // handles line received		
        private void Handle_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            _commsQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessLineReceived));
        }

        // processes line received
        private void ProcessLineReceived(string lineReceived)
        {
            if (string.IsNullOrEmpty(lineReceived)) return;
            Debug.Console(2, this, "ProcessLinereceived: lineReceived = {0}", lineReceived);

            Match responses = _regexPattern.Match(lineReceived);
            char[] trimPattern = { '{', '}', ' ' };

            string indexString = responses.Groups["Index"].Value.Trim();
            string command = responses.Groups["Command"].Value.Trim();
            string state = responses.Groups["State"].Value.Trim(trimPattern);

            if (string.IsNullOrEmpty(command)) return;

            Debug.Console(2, this, "ProcessLinereceived: index-'{0}' | command-'{1} | state-'{2}'", indexString,
                command, state);

            switch (command)
            {
                // Battery percent charge
                // TX: < GET x BATT_CHARGE >
                // RX: < REP x BATT_CHARGE 027 >
                case "BATT_CHARGE":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index >= 0 && index < SbcSize)
                    {
                        short stateInt = Convert.ToInt16(state);
                        if (stateInt >= 0 && stateInt <= 100)
                            Microphones[index].PercentCharge = stateInt;
                        else
                            Microphones[index].PercentCharge = 0;
                    }

                    break;
                }
                // Battery percent health
                // TX: < GET x BATT_HEALTH >
                // RX: < REP x BATT_CHARGE 099 >
                case "BATT_HEALTH":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index >= 0 && index < SbcSize)
                    {
                        short stateInt = Convert.ToInt16(state);
                        if (stateInt >= 0 && stateInt <= 100)
                            Microphones[index].PercentHealth = stateInt;
                        else
                            Microphones[index].PercentHealth = 0;
                    }

                    break;
                }
                // Battery temperature F
                // TX: < GET x BATT_TEMP_F >
                // RX: < REP x BATT_TEMP_F 095 >
                case "BATT_TEMP_F":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index >= 0 && index < SbcSize)
                    {
                        short stateInt = Convert.ToInt16(state);
                        if (stateInt >= 0 && stateInt <= 253)
                            Microphones[index].TemperatureF = stateInt;
                        else
                            Microphones[index].TemperatureF = 0;
                    }

                    break;
                }

                // Battery error
                // TX: < GET x BATT_ERROR >
                // RX: < REP x BATT_ERROR 000 >
                case "BATT_ERROR":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    short stateInt = Convert.ToInt16(state);
                    if (index >= 0 && index < SbcSize)
                    {
                        Microphones[index].BatteryError = stateInt;
                        switch (stateInt)
                        {
                            case 0:
                                Microphones[index].ErrorString = "No Active Error";
                                break;
                            case 1:
                                Microphones[index].ErrorString = "Unknown Module";
                                break;
                            case 2:
                                Microphones[index].ErrorString = "Unrecognized Battery";
                                break;
                            case 3:
                                Microphones[index].ErrorString = "Deep Discharge Recovery Failed";
                                break;
                            case 4:
                                Microphones[index].ErrorString = "Charge Failed";
                                break;
                            case 5:
                                Microphones[index].ErrorString = "Check Battery";
                                break;
                            case 6:
                                Microphones[index].ErrorString = "Check Charger";
                                break;
                            case 7:
                                Microphones[index].ErrorString = "Communication Failure";
                                break;
                            case 255:
                                //Supposed to be "No battery present" but appears to always send 255 on firmware 1.4.7.0 even with battery present
                                Microphones[index].ErrorString = "No Active Error";
                                break;
                            default:
                                Microphones[index].ErrorString = "Unknown Error";
                                break;
                        }
                    }

                    break;
                }

                // Battery state
                // TX: < GET x BATT_STATE >
                // RX: < REP x BATT_STATE NORMAL >
                case "BATT_STATE":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index >= 0 && index < SbcSize) Microphones[index].BatteryState = state;

                    break;
                }
                // Model Number
                // TX: "< GET MODEL >"
                // RX: "< REP MODEL {y} >"	// y is 32-char model number
                case "MODEL":
                {
                    DeviceModel = state;
                    break;
                }
                // Firmware Version
                // TX: "< GET FW_VER >"
                // RX: "< REP FW_VER {y} >" // y is 18-char firmware version
                case "FW_VER":
                {
                    DeviceFirmwareVersion = state;
                    for (ushort i = 0; i < SbcSize; i++) Microphones[i].DeviceFirmwareVersion = state;
                    break;
                }
                default:
                {
                    Debug.Console(1, this, "ProcessLineReceived: Unkown command-'{0}' with state-'{1}'", command,
                        state);
                    break;
                }
            }
        }

        /// <summary>
        /// Sends text to the device plugin comms
        /// </summary>
        /// <param name="text">Command to be sent</param>		
        public void SendText(string text)
        {
            if (!_comms.IsConnected) return;

            if (string.IsNullOrEmpty(text)) return;

            string cmd = string.Format("< {0} >", text.ToUpper());

            Debug.Console(1, this, "SendText: {0}", cmd);
            _comms.SendText(cmd);
        }

        #region Polls

        /// <summary>
        /// Polls the device
        /// </summary>
        /// <remarks>
        /// Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
        /// </remarks>
        public void Poll()
        {
            SendText("GET 0 BATT_MODULE_TYPE");
        }

        #endregion Polls


        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            try
            {
                MicControllerJoinMap joinMap = new MicControllerJoinMap(joinStart);

                // This adds the join map to the collection on the bridge
                if (bridge != null) bridge.AddJoinMap(Key, joinMap);

                Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
                Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

                // links to bridge
                trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
                trilist.SetSigTrueAction(joinMap.RefreshData.JoinNumber, UpdateStatus);

                // _commsMonitor.IsOnlineFeedback is used to drive IsOnlineFb on the bridge
                _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
                SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);
                MonitorStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.MonitorStatus.JoinNumber]);

                // device information feedback
                DeviceModelFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceModel.JoinNumber]);
                DeviceFirmwareVersionFeedback.LinkInputSig(
                    trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);

                UpdateFeedbacks();

                trilist.OnlineStatusChange += (o, a) =>
                {
                    if (!a.DeviceOnLine) return;
                    trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
                    UpdateFeedbacks();
                };
            }
            catch (Exception ex)
            {
                Debug.ConsoleWithLog(0, "Exception Linking to Bridge Type {0}: {1}", GetType().Name, ex.Message);
            }
        }

        private void UpdateFeedbacks()
        {
            SocketStatusFeedback.FireUpdate();
            MonitorStatusFeedback.FireUpdate();
            DeviceModelFeedback.FireUpdate();
            DeviceFirmwareVersionFeedback.FireUpdate();
            for (ushort i = 0; i < SbcSize; i++) Microphones[i].FireUpdate();
        }

        #endregion Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Update status of all parameters
        /// Shure command string API recommends running this command on first power up
        /// </summary>
        public void UpdateStatus()
        {
            SendText("GET 0 ALL");
        }
    }

    public class ShureSbcBattery : WirelessMic
    {
        public ShureSbcBattery()
            : this(Guid.NewGuid().ToString(), "Shure SBC Battery")
        {
        }

        public ShureSbcBattery(string key, string name)
            : base(key, name, true)
        {
            Model = "Shure Battery";
        }

        public bool BatteryPresent
        {
            get { return OnDock; }
            set
            {
                OnDock = value;
                if (!value) PercentHealth = 0;
            }
        }

        public int BatteryError
        {
            get { return BatteryErrorAnalog; }
            set { BatteryErrorAnalog = value; }
        }

        public IntFeedback BatteryErrorFeedback
        {
            get { return BatteryErrorAnalogFeedback; }
        }

        public string BatteryErrorText
        {
            get { return ErrorString; }
            set { ErrorString = value; }
        }

        public StringFeedback BatteryErrorTextFeedback
        {
            get { return ErrorStringFeedback; }
        }

        public string BatteryState
        {
            get { return State; }
            set
            {
                State = value;
                BatteryPresent = !string.IsNullOrEmpty(value) &&
                                 !value.Equals("NO_BATT", StringComparison.OrdinalIgnoreCase);
            }
        }

        public StringFeedback BatteryStateFeedback
        {
            get { return StateFeedback; }
        }
    }

    /// <summary>
    /// Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class ShureSbcFactory : EssentialsDeviceFactory<ShureSbcDevice>
    {
        /// <summary>
        /// Device factory constructor
        /// </summary>
        public ShureSbcFactory()
        {
            TypeNames = new List<string> { "shuresbc" };
        }

        /// <summary>
        /// Builds and returns an instance of ShureMxaDevice
        /// </summary>
        /// <param name="dc">device configuration</param>
        /// <returns>plugin device or null</returns>
        /// <seealso cref="PepperDash.Core.eControlMethod"/>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            try
            {
                Debug.Console(0, "[{0}] Factory attempting to create new device from type: {1}", dc.Key, dc.Type);

                MicController propertiesConfig = dc.Properties.ToObject<MicController>();

                // build the device comms (for all other comms methods) & check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new ShureSbcDevice(dc.Key, dc.Name, propertiesConfig, comms);
                Debug.Console(0, "[{0}] Factory: failed to create comm for {1}", dc.Key, dc.Name);
                return null;
            }
            catch (Exception ex)
            {
                Debug.Console(0, "[{0}] Factory BuildDevice Exception: {1}", dc.Key, ex);
                return null;
            }
        }
    }
}