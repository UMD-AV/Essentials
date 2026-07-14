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
    public class ShureMxwapxd2Device : EssentialsBridgeableDevice
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";
        private readonly GenericQueue _commsQueue;
        public int Mxwapxd2Size { get; private set; }
        public readonly WirelessMic[] Microphones;

        private readonly Regex _regexPattern = new Regex(
            @"< REP CH (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >",
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

        // device firmware version field
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


        /// <summary>
        /// Device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <param name="config"></param>
        /// <see cref="PepperDash.Core.IBasicCommunication"/>
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        public ShureMxwapxd2Device(string key, string name,
            IBasicCommunication comms, MicController config)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            MonitorStatusFeedback = new IntFeedback(() =>
            {
                if (_commsMonitor != null) return (int)_commsMonitor.Status;
                return 0;
            });
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            Mxwapxd2Size = 2;
            Microphones = new WirelessMic[config.MicKeys.Length];
            for (ushort i = 0; i < config.MicKeys.Length; i++)
            {
                Microphones[i] = new WirelessMic(config.MicKeys[i], config.MicKeys[i])
                {
                    Model = "Shure Mxw Tx"
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
            }

            _comms = comms;
            _commsGather = new CommunicationGather(_comms, CommsDelimiter)
                { IncludeDelimiter = true };
            _commsGather.LineReceived += Handle_LineReceived;
            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _commsMonitor.StatusChange += (sender, args) =>
            {
                foreach (WirelessMic mic in Microphones) mic.IsOnline = args.Status == MonitorStatus.IsOk;
            };
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
                // Tx state
                // TX: < GET x TX_STATUS >
                // RX: < REP x TX_STATUS ON_CHARGER >
                case "TX_STATUS":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index < Mxwapxd2Size)
                    {
                        WirelessMic mic = Microphones[index];
                        switch (state.ToUpper())
                        {
                            case "ACTIVE":
                            {
                                mic.LinkState = LinkStates.Connected;
                                mic.MuteState = false;
                                break;
                            }
                            case "MUTED":
                            {
                                mic.LinkState = LinkStates.Connected;
                                mic.MuteState = true;
                                break;
                            }
                            case "OFF":
                            {
                                mic.LinkState = LinkStates.Disconnected;
                                mic.MuteState = true;
                                break;
                            }
                            case "ON_CHARGER":
                            {
                                mic.LinkState = LinkStates.Charging;
                                mic.MuteState = true;
                                break;
                            }
                            case "UNKNOWN":
                            {
                                mic.LinkState = LinkStates.Unknown;
                                mic.MuteState = true;
                                break;
                            }
                        }
                    }

                    break;
                }

                // Battery percent charge
                // TX: < GET x BATT_CHARGE >
                // RX: < REP x BATT_CHARGE 027 >
                case "BATT_CHARGE":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index < Mxwapxd2Size)
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
                    if (index < Mxwapxd2Size)
                    {
                        short stateInt = Convert.ToInt16(state);
                        if (stateInt >= 0 && stateInt <= 100)
                            Microphones[index].PercentHealth = stateInt;
                        else
                            Microphones[index].PercentHealth = 0;
                    }

                    break;
                }

                // Battery run time
                // TX: < GET x RUN_TIME >
                // RX: < REP x RUN_TIME 65535 >
                case "BATT_RUN_TIME":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index < Mxwapxd2Size)
                    {
                        ushort stateInt = Convert.ToUInt16(state);
                        Microphones[index].Runtime = stateInt;
                    }

                    break;
                }

                case "CHAN_NAME":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index < Mxwapxd2Size) Microphones[index].Name = state;

                    break;
                }
                // Firmware Version
                // TX: "< GET FW_VER >"
                // RX: "< REP FW_VER {y} >" // y is 18-char firmware version
                case "FW_VER":
                {
                    DeviceFirmwareVersion = state;
                    for (ushort i = 0; i < Mxwapxd2Size; i++) Microphones[i].DeviceFirmwareVersion = state;

                    break;
                }
                // Device Model
                // TX: "< GET DEVICE_MODEL >"
                // RX: "< REP DEVICE_MODEL {y} >" // y is up to 31 character device name
                case "DEVICE_MODEL":
                {
                    DeviceModel = state;
                    break;
                }
                // Tx Model
                case "TX_MODEL":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index < Mxwapxd2Size)
                    {
                        Microphones[index].Model = state;
                        if (state == "MXW1X")
                            Microphones[index].Name = "MXW Bodypack";
                        else if (state == "MXW2X") Microphones[index].Name = "MXW Handheld";
                    }

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
        public void Poll()
        {
            SendText("GET 0 TX_STATUS");
            SendText("GET 0 BATT_HEALTH");
            SendText("GET 0 BATT_CHARGE");
            SendText("GET 0 BATT_RUN_TIME");
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
                DeviceFirmwareVersionFeedback.LinkInputSig(
                    trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);
                DeviceModelFeedback.LinkInputSig(
                    trilist.StringInput[joinMap.DeviceModel.JoinNumber]);
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
            DeviceFirmwareVersionFeedback.FireUpdate();
            DeviceModelFeedback.FireUpdate();
            MicControllerUtilities.FireMicrophoneFeedbacks(Microphones);
        }

        #endregion Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Update status of all parameters
        /// Shure command string API recommends running this command on first power up
        /// </summary>
        public void UpdateStatus()
        {
            SendText("GET ALL");
        }
    }

    /// <summary>
    /// Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class ShureMxwapxd2Factory : EssentialsDeviceFactory<ShureMxwapxd2Device>
    {
        /// <summary>
        /// Device factory constructor
        /// </summary>
        public ShureMxwapxd2Factory()
        {
            TypeNames = new List<string> { "shureMxwapxd2" };
        }

        /// <summary>
        /// Builds and returns an instance of ShureMxwapxd2Device
        /// </summary>
        /// <param name="dc">device configuration</param>
        /// <returns>plugin device or null</returns>
        /// <seealso cref="PepperDash.Core.eControlMethod"/>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            try
            {
                Debug.Console(0, "[{0}] Factory attempting to create new device from type: {1}", dc.Key, dc.Type);

                MicController properties = dc.Properties.ToObject<MicController>();

                // build the device comms (for all other comms methods) & check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new ShureMxwapxd2Device(dc.Key, dc.Name, comms, properties);
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