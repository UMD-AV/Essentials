using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Queues;

namespace PepperDash.Essentials.Devices.Common.ShureSbc
{
    public class ShureSbcDevice : EssentialsBridgeableDevice, IHasErrorString
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";
        private readonly GenericQueue _commsQueue;
        public int SbcSize { get; private set; }
        public ShureSbcBattery[] Batteries;
        private CTimer batteryCheckTimer;

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
        /// Deivce error feedback
        /// </summary>
        public StringFeedback ErrorFeedback { get; private set; }

        // battery check ran field
        private bool _batteryCheckRan5AM;
        /// <summary>
        /// battery check ran property
        /// </summary>
        public bool BatteryCheckRan5AM
        {
            get { return _batteryCheckRan5AM; }
            set
            {
                _batteryCheckRan5AM = value;
                BatteryCheckRan5AMFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery check ran feedback
        /// </summary>
        public BoolFeedback BatteryCheckRan5AMFeedback { get; private set; }
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
        public ShureSbcDevice(string key, string name, ShureSbcPropertiesConfig config, IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            MonitorStatusFeedback = new IntFeedback(() => (int)_commsMonitor.Status);
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            ErrorFeedback = new StringFeedback(() => DeviceError);
            BatteryCheckRan5AMFeedback = new BoolFeedback(() => BatteryCheckRan5AM);

            SbcSize = config.size <= 8 ? config.size : 8;
            Batteries = new ShureSbcBattery[8];
            for (ushort i = 0; i < 8; i++)
            {
                Batteries[i] = new ShureSbcBattery();
            }

            _comms = comms;
            var commsGather = new CommunicationGather(_comms, CommsDelimiter) { IncludeDelimiter = true };
            commsGather.LineReceived += Handle_LineRecieved;
            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _commsQueue = new GenericQueue(key + "-queue");

            var socket = _comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is IP **ELSE** device comms is RS232
                socket.ConnectionChange += socket_ConnectionChange;
                SocketStatusFeedback = new IntFeedback(() => (int)socket.ClientStatus);
            }
        }

        /// <summary>
        /// Use the custom activiate to connect the device and start the comms monitor.
        /// This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {                      
            _comms.Connect();
            _commsMonitor.Start();
            batteryCheckTimer = new CTimer(batteryCheckTimerCallback, Crestron.SimplSharp.Timeout.Infinite);
            armBatteryCheckTimer();

            return base.CustomActivate();
        }

        private void armBatteryCheckTimer()
        {
            //Try to arm check for 5 AM
            //This will typically run at 4 AM then adjust to 5 AM
            //The purpose of running at 4 AM is to check in case of DST that we didn't jump forward 1 hour
            DateTime now = DateTime.Now;
            DateTime fiveAM = DateTime.Today.AddHours(5);

            if (now >= fiveAM)
            {
                fiveAM = fiveAM.AddHours(23);
            }

            int timeUntilFourAM = (int)(fiveAM - now).TotalMilliseconds + 10000;
            batteryCheckTimer.Reset(timeUntilFourAM);
        }

        private void batteryCheckTimerCallback(object o)
        {
            armBatteryCheckTimer();
            BatteryCheckRan5AM = false;

            if ((DateTime.Now > DateTime.Today.AddHours(5)) && isWeekday(DateTime.Today.DayOfWeek))
            {
                
                int count = 0;
                foreach (ShureSbcBattery b in Batteries)
                {
                    b.BatteryPresent5AM = b.BatteryPresent;
                    if (b.BatteryPresent)
                    {
                        count++;
                    }
                }
                Debug.ConsoleWithLog(0, "5 AM battery check found {0} batteries", count);
                CrestronEnvironment.Sleep(1000);
                BatteryCheckRan5AM = true;
            }
        }

        private bool isWeekday(DayOfWeek day)
        {
            if (day == DayOfWeek.Monday ||
                day == DayOfWeek.Tuesday ||
                day == DayOfWeek.Wednesday ||
                day == DayOfWeek.Thursday ||
                day == DayOfWeek.Friday)
            {
                return true;
            }
            return false;
        }

        // socket connection change event handler
        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.FireUpdate();

            if (args.Client.IsConnected)
                UpdateStatus();
        }

        // handles line recieved		
        private void Handle_LineRecieved(object sender, GenericCommMethodReceiveTextArgs args)
        {
            _commsQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessLineRecieved));
        }

        // processes line recieved
        private void ProcessLineRecieved(string lineRecieved)
        {
            if (string.IsNullOrEmpty(lineRecieved)) return;
            Debug.Console(2, this, "ProcessLineRecieved: lineReceived = {0}", lineRecieved);

            var regexPattern = new Regex(@"< REP (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >", RegexOptions.IgnoreCase);
            var responses = regexPattern.Match(lineRecieved);
            if (responses == null) return;
            char[] trimPattern = { '{', '}', ' ' };

            var indexString = responses.Groups["Index"].Value.Trim();
            var command = responses.Groups["Command"].Value.Trim();
            var state = responses.Groups["State"].Value.Trim(trimPattern);

            if (string.IsNullOrEmpty(command)) return;

            Debug.Console(2, this, "ProcessLineRecieved: index-'{0}' | command-'{1} | state-'{2}'", indexString, command, state);

            switch (command)
            {
                // Battery percent charge
                // TX: < GET x BATT_CHARGE >
                // RX: < REP x BATT_CHARGE 027 >
                case "BATT_CHARGE":
                    {
                        var index = Convert.ToInt16(indexString) - 1;                        
                        if (index < 8)
                        {
                            var stateInt = Convert.ToInt16(state);
                            if (stateInt >= 0 && stateInt <= 100)
                            {
                                Batteries[index].PercentCharge = stateInt;
                            }
                            else
                            {
                                Batteries[index].PercentCharge = 0;
                            }
                        }
                        break;
                    }
                // Battery percent health
                // TX: < GET x BATT_HEALTH >
                // RX: < REP x BATT_CHARGE 099 >
                case "BATT_HEALTH":
                    {
                        var index = Convert.ToInt16(indexString) - 1;
                        if (index < 8)
                        {
                            var stateInt = Convert.ToInt16(state);
                            if (stateInt >= 0 && stateInt <= 100)
                            {
                                Batteries[index].PercentHealth = stateInt;
                            }
                            else
                            {
                                Batteries[index].PercentHealth = 0;
                            }
                        }
                        break;
                    }
                // Battery temperature F
                // TX: < GET x BATT_TEMP_F >
                // RX: < REP x BATT_TEMP_F 095 >
                case "BATT_TEMP_F":
                    {
                        var index = Convert.ToInt16(indexString) - 1;
                        if (index < 8)
                        {
                            var stateInt = Convert.ToInt16(state);
                            if (stateInt >= 0 && stateInt <= 253)
                            {
                                Batteries[index].TemperatureF = stateInt;
                            }
                            else
                            {
                                Batteries[index].TemperatureF = 0;
                            }
                        }
                        break;
                    }

                // Battery error
                // TX: < GET x BATT_ERROR >
                // RX: < REP x BATT_ERROR 000 >
                case "BATT_ERROR":
                    {
                        var index = Convert.ToInt16(indexString) - 1;
                        var stateInt = Convert.ToInt16(state);
                        if (index < 8)
                        {
                            Batteries[index].BatteryError = stateInt;
                            switch (stateInt)
                            {
                                case 0:
                                    Batteries[index].BatteryErrorText = "No Active Error";
                                    break;
                                case 1:
                                    Batteries[index].BatteryErrorText = "Unknown Module";
                                    break;
                                case 2:
                                    Batteries[index].BatteryErrorText = "Unrecognized Battery";
                                    break;
                                case 3:
                                    Batteries[index].BatteryErrorText = "Deep Discharge Recovery Failed";
                                    break;
                                case 4:
                                    Batteries[index].BatteryErrorText = "Charge Failed";
                                    break;
                                case 5:
                                    Batteries[index].BatteryErrorText = "Check Battery";
                                    break;
                                case 6:
                                    Batteries[index].BatteryErrorText = "Check Charger";
                                    break;
                                case 7:
                                    Batteries[index].BatteryErrorText = "Communication Failure";
                                    break;
                                case 255:
                                    //Supposed to be "No battery present" but appears to always send 255 on firmware 1.4.7.0 even with battery present
                                    Batteries[index].BatteryErrorText = "No Active Error";
                                    break;
                                default:
                                    Batteries[index].BatteryErrorText = "Unknown Error";
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
                        var index = Convert.ToInt16(indexString) - 1;
                        if (index < 8)
                        {
                            Batteries[index].BatteryState = state;
                        }
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
                        break;
                    }
                case "BATT_MODULE_TYPE":
                    {
                        var index = Convert.ToInt16(indexString) - 1;
                        if (index < 4)
                        {
                            var stateInt = Convert.ToInt16(state);
                            Batteries[index*2].BatteryEnabled = (stateInt > 0);
                            Batteries[index*2 + 1].BatteryEnabled = (stateInt > 0);
                        }
                        break;
                    }
                default:
                    {
                        Debug.Console(1, this, "ProcessLineReceived: Unkown command-'{0}' with state-'{1}'", command, state);
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
            if (_comms.IsConnected == false) return;

            if (string.IsNullOrEmpty(text)) return;

            var cmd = string.Format("< {0} >", text.ToUpper());

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
                var joinMap = new ShureSbcBridgeJoinMap(joinStart);

                // This adds the join map to the collection on the bridge
                if (bridge != null)
                {
                    bridge.AddJoinMap(Key, joinMap);
                }

                Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
                Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

                // links to bridge
                trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
                trilist.SetSigTrueAction(joinMap.RefreshData.JoinNumber, UpdateStatus);

                // _commsMonitor.IsOnlineFeedback is used to drive IsOnlineFb on the bridge
                _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
                SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);
                MonitorStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.MonitorStatus.JoinNumber]);
                BatteryCheckRan5AMFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Battery5AMCheckRan.JoinNumber]);

                // battery info **feedback only**
                for (ushort i = 0; i < 8; i++)
                {
                    ushort index = i;
                    Batteries[index].BatteryEnabledFeedback.LinkInputSig(trilist.BooleanInput[joinMap.BatteryEnabled.JoinNumber + index]);
                    Batteries[index].BatteryPresentFeedback.LinkInputSig(trilist.BooleanInput[joinMap.BatteryPresent.JoinNumber + index]);
                    Batteries[index].BatteryPresent5AMFeedback.LinkInputSig(trilist.BooleanInput[joinMap.BatteryPresent5AM.JoinNumber + index]);
                    Batteries[index].PercentChargeFeedback.LinkInputSig(trilist.UShortInput[joinMap.PercentCharge.JoinNumber + index]);
                    Batteries[index].PercentHealthFeedback.LinkInputSig(trilist.UShortInput[joinMap.PercentHealth.JoinNumber + index]);
                    Batteries[index].TemperatureFFeedback.LinkInputSig(trilist.UShortInput[joinMap.TemperatureF.JoinNumber + index]);
                    Batteries[index].BatteryErrorFeedback.LinkInputSig(trilist.UShortInput[joinMap.BatteryError.JoinNumber + index]);
                    Batteries[index].BatteryErrorTextFeedback.LinkInputSig(trilist.StringInput[joinMap.BatteryErrorText.JoinNumber + index]);
                    Batteries[index].BatteryStateFeedback.LinkInputSig(trilist.StringInput[joinMap.BatteryStateText.JoinNumber + index]);
                }

                // device information feedback
                DeviceModelFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceModel.JoinNumber]);
                DeviceFirmwareVersionFeedback.LinkInputSig(trilist.StringInput[joinMap.DeviceFirmwareVersion.JoinNumber]);

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

            for (ushort i = 0; i < 8; i++)
            {
                Batteries[i].BatteryEnabledFeedback.FireUpdate();
                Batteries[i].BatteryPresentFeedback.FireUpdate();
                Batteries[i].BatteryPresent5AMFeedback.FireUpdate();
                Batteries[i].PercentChargeFeedback.FireUpdate();
                Batteries[i].PercentHealthFeedback.FireUpdate();
                Batteries[i].TemperatureFFeedback.FireUpdate();
                Batteries[i].BatteryErrorFeedback.FireUpdate();
                Batteries[i].BatteryErrorTextFeedback.FireUpdate();
                Batteries[i].BatteryStateFeedback.FireUpdate();
            }

            BatteryCheckRan5AMFeedback.FireUpdate();
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

    public class ShureSbcBattery
    {
        #region Battery Enabled
        private bool _batteryEnabled;
        public bool BatteryEnabled
        {
            get { return _batteryEnabled; }
            set
            {
                _batteryEnabled = value;
                BatteryEnabledFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery enabled feedback
        /// </summary>
        public BoolFeedback BatteryEnabledFeedback { get; private set; }
        #endregion

        #region Battery Present
        private bool _batteryPresent;
        public bool BatteryPresent
        {
            get { return _batteryPresent; }
            set
            {
                _batteryPresent = value;
                BatteryPresentFeedback.FireUpdate();

                //Fix for issue where battery health persists after battery disappears
                if (!_batteryPresent)
                {
                    PercentHealth = 0;
                }
            }
        }
        /// <summary>
        /// Battery present feedback
        /// </summary>
        public BoolFeedback BatteryPresentFeedback { get; private set; }
        #endregion

        #region Battery Present 5AM
        private bool _batteryPresent5AM = false;
        public bool BatteryPresent5AM
        {
            get { return _batteryPresent5AM; }
            set
            {
                _batteryPresent5AM = value;
                BatteryPresent5AMFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery present 5AM feedback
        /// </summary>
        public BoolFeedback BatteryPresent5AMFeedback { get; private set; }
        #endregion

        #region Percent Charge (BATT_CHARGE)
        private int _percentCharge;
        public int PercentCharge
        {
            get { return _percentCharge; }
            set
            {
                _percentCharge = value;
                PercentChargeFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery percent charge feedback
        /// </summary>
        public IntFeedback PercentChargeFeedback { get; private set; }
        #endregion

        #region Percent Health (BATT_HEALTH)
        private int _percentHealth;
        public int PercentHealth
        {
            get { return _percentHealth; }
            set
            {
                if (value > 0)
                {
                    _percentHealth = value;
                    PercentHealthFeedback.FireUpdate();
                }
            }
        }
        /// <summary>
        /// Battery percent health feedback
        /// </summary>
        public IntFeedback PercentHealthFeedback { get; private set; }
        #endregion

        #region TemperatureF (BATT_TEMP_F)
        private int _temperatureF;
        public int TemperatureF
        {
            get { return _temperatureF; }
            set
            {
                _temperatureF = value;
                TemperatureFFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery temperature in F feedback
        /// </summary>
        public IntFeedback TemperatureFFeedback { get; private set; }
        #endregion

        #region Battery Error (BATT_ERROR)
        private int _batteryError;
        public int BatteryError
        {
            get { return _batteryError; }
            set
            {
                _batteryError = value;
                BatteryErrorFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery error feedback
        /// </summary>
        public IntFeedback BatteryErrorFeedback { get; private set; }
        #endregion

        #region Battery Error Text (BATT_ERROR)
        private string _batteryErrorText;
        public string BatteryErrorText
        {
            get { return _batteryErrorText; }
            set
            {
                _batteryErrorText = value;
                BatteryErrorTextFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery error text feedback
        /// </summary>
        public StringFeedback BatteryErrorTextFeedback { get; private set; }
        #endregion

        #region Battery State (BATT_STATE)
        private string _batteryState;
        public string BatteryState
        {
            get { return _batteryState; }
            set
            {
                _batteryState = value;
                BatteryPresent = (value.Length > 0 && value != "NO_BATT");
                BatteryStateFeedback.FireUpdate();
            }
        }
        /// <summary>
        /// Battery state message feedback
        /// </summary>
        public StringFeedback BatteryStateFeedback { get; private set; }
        #endregion

        public ShureSbcBattery()
        {
            BatteryEnabledFeedback = new BoolFeedback(() => BatteryEnabled);
            BatteryPresentFeedback = new BoolFeedback(() => BatteryPresent);
            BatteryPresent5AMFeedback = new BoolFeedback(() => BatteryPresent5AM);
            PercentChargeFeedback = new IntFeedback(() => PercentCharge);
            PercentHealthFeedback = new IntFeedback(() => PercentHealth);
            TemperatureFFeedback = new IntFeedback(() => TemperatureF);
            BatteryErrorFeedback = new IntFeedback(() => BatteryError);
            BatteryErrorTextFeedback = new StringFeedback(() => BatteryErrorText);
            BatteryStateFeedback = new StringFeedback(() => BatteryState);
        }
    }

    public class ShureSbcBridgeJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        /// <summary>
        /// Get device online feedback
        /// </summary>
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Is Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Refresh all data
        /// </summary>
        [JoinName("RefreshData")]
        public JoinDataComplete RefreshData = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh all battery data",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get enabled feedback for a battery
        /// </summary>
        [JoinName("BatteryEnabled")]
        public JoinDataComplete BatteryEnabled = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Enabled feedback for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get present feedback for a battery
        /// </summary>
        [JoinName("BatteryPresent")]
        public JoinDataComplete BatteryPresent = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Present feedback for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get present feedback for a battery at 5AM
        /// </summary>
        [JoinName("BatteryPresent5AM")]
        public JoinDataComplete BatteryPresent5AM = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 31,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Present feedback at 5AM for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Report battery check ran at 5AM
        /// </summary>
        [JoinName("Battery5AMCheckRan")]
        public JoinDataComplete Battery5AMCheckRan = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 40,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Report if battery check at 5AM ran already",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });
        #endregion

        #region Analog

        /// <summary>
        /// Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        [JoinName("SocketStatus")]
        public JoinDataComplete SocketStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Socket SocketStatus",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get device monitor status join map
        /// </summary>
        /// <see cref="PepperDash.Essentials.Core.MonitorStatus"/>
        [JoinName("MonitorStatus")]
        public JoinDataComplete MonitorStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Monitor Status",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get percent charge for a battery
        /// </summary>
        /// <remarks>
        /// 000-100 = percent charge,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("PercentCharge")]
        public JoinDataComplete PercentCharge = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Percent charge for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get health for a battery
        /// </summary>
        /// <remarks>
        /// 000-100 = percent health,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("PercentHealth")]
        public JoinDataComplete PercentHealth = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Percent health for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get temperature in F for a battery
        /// </summary>
        /// <remarks>
        /// 000-253 = temperature in F,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("TemperatureF")]
        public JoinDataComplete TemperatureF = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 31,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Temperature in F for a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get battery error
        /// </summary>
        [JoinName("BatteryError")]
        public JoinDataComplete BatteryError = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 41,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Battery Error",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion

        #region Serial

        /// <summary>
        /// Get device name
        /// </summary>
        [JoinName("DeviceName")]
        public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get device model
        /// </summary>
        [JoinName("DeviceModel")]
        public JoinDataComplete DeviceModel = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Model",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get device firmware version
        /// </summary>
        [JoinName("DeviceFirmwareVersion")]
        public JoinDataComplete DeviceFirmwareVersion = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Firmware Version",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get battery error text
        /// </summary>
        [JoinName("BatteryErrorText")]
        public JoinDataComplete BatteryErrorText = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Battery Error Text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        /// Get battery state text
        /// </summary>
        [JoinName("BatteryStateText")]
        public JoinDataComplete BatteryStateText = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Battery State Text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });


        #endregion

        /// <summary>
        /// Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public ShureSbcBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(ShureSbcBridgeJoinMap))
        {
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
            // In the constructor we initialize the list with the typenames that will build an instance of this device
            // only include unique typenames, when the constructur is used all the typenames will be evaluated in lower case.
            TypeNames = new List<string>() { "shuresbc" };
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

                // get the device properties configuration object & check for null 
                var propertiesConfig = dc.Properties.ToObject<ShureSbcPropertiesConfig>();
                if (propertiesConfig == null)
                {
                    Debug.Console(0, "[{0}] Factory: failed to read properties config for {1}", dc.Key, dc.Name);
                    return null;
                }

                // build the device comms (for all other comms methods) & check for null			
                var comms = CommFactory.CreateCommForDevice(dc);
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

    public class ShureSbcPropertiesConfig
    {
        [JsonProperty("size")]
        public int size { get; set; }

        public ShureSbcPropertiesConfig()
        {
        }
    }
}