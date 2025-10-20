using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Queues;

namespace PepperDash.Essentials.Devices.Common.ShureMxwapxd2
{
    public class ShureMxwapxd2Device : EssentialsBridgeableDevice
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";
        private readonly GenericQueue _commsQueue;
        public int Mxwapxd2Size { get; private set; }
        public readonly ShureMxwTx[] Txs;
        private CTimer TxCheckTimer;

        private readonly Regex regexPattern = new Regex(
            @"< REP CH (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >",
            RegexOptions.IgnoreCase);

        private readonly CommunicationGather commsGather;

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

        // Tx check ran field
        private bool _TxCheckRan5AM;

        /// <summary>
        /// Tx check ran property
        /// </summary>
        public bool TxCheckRan5AM
        {
            get { return _TxCheckRan5AM; }
            set
            {
                _TxCheckRan5AM = value;
                TxCheckRan5AMFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Tx check ran feedback
        /// </summary>
        public BoolFeedback TxCheckRan5AMFeedback { get; private set; }

        /// <summary>
        /// Device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication"/>
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        public ShureMxwapxd2Device(string key, string name,
            IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            MonitorStatusFeedback = new IntFeedback(() =>
            {
                if (_commsMonitor != null) return (int)_commsMonitor.Status;
                return 0;
            });
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            TxCheckRan5AMFeedback = new BoolFeedback(() => TxCheckRan5AM);

            Mxwapxd2Size = 2;
            Txs = new ShureMxwTx[Mxwapxd2Size];
            for (ushort i = 0; i < Mxwapxd2Size; i++)
            {
                Txs[i] = new ShureMxwTx();
                Txs[i].TxEnabled = true;
            }

            _comms = comms;
            commsGather = new CommunicationGather(_comms, CommsDelimiter)
                { IncludeDelimiter = true };
            commsGather.LineReceived += Handle_LineReceived;
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
            TxCheckTimer = new CTimer(TxCheckTimerCallback, Timeout.Infinite);
            armTxCheckTimer();

            return base.CustomActivate();
        }

        private void armTxCheckTimer()
        {
            //Try to arm check for 5 AM
            //This will typically run at 4 AM then adjust to 5 AM.
            //The purpose of running at 4 AM is to check in case of DST that we didn't jump forward 1 hour
            DateTime now = DateTime.Now;
            DateTime fiveAM = DateTime.Today.AddHours(5);

            if (now >= fiveAM)
            {
                fiveAM = fiveAM.AddHours(23);
            }

            int timeUntilFourAM = (int)(fiveAM - now).TotalMilliseconds + 10000;
            TxCheckTimer.Reset(timeUntilFourAM);
        }

        private void TxCheckTimerCallback(object o)
        {
            armTxCheckTimer();
            TxCheckRan5AM = false;

            if ((DateTime.Now > DateTime.Today.AddHours(5)) && isWeekday(DateTime.Today.DayOfWeek))
            {
                int count = 0;
                foreach (ShureMxwTx b in Txs)
                {
                    b.TxPresent5AM = b.TxPresent;
                    if (b.TxPresent)
                    {
                        count++;
                    }
                }

                Debug.ConsoleWithLog(0, "5 AM Tx check found {0} Txs", count);
                CrestronEnvironment.Sleep(1000);
                TxCheckRan5AM = true;
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

            Match responses = regexPattern.Match(lineReceived);
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
                        Txs[index].TxStatus = state;
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
                        {
                            Txs[index].PercentCharge = stateInt;
                        }
                        else
                        {
                            Txs[index].PercentCharge = 0;
                        }
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
                        {
                            Txs[index].PercentHealth = stateInt;
                        }
                        else
                        {
                            Txs[index].PercentHealth = 0;
                        }
                    }

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
            if (_comms.IsConnected == false) return;

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
                ShureMxwapxd2BridgeJoinMap joinMap = new ShureMxwapxd2BridgeJoinMap(joinStart);

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
                TxCheckRan5AMFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Docked5AMCheckRan.JoinNumber]);

                // Tx info **feedback only**
                for (ushort i = 0; i < Mxwapxd2Size; i++)
                {
                    Txs[i].TxEnabledFeedback
                        .LinkInputSig(trilist.BooleanInput[joinMap.TxEnabled.JoinNumber + i]);
                    Txs[i].TxPresentFeedback
                        .LinkInputSig(trilist.BooleanInput[joinMap.TxDocked.JoinNumber + i]);
                    Txs[i].TxPresent5AMFeedback
                        .LinkInputSig(trilist.BooleanInput[joinMap.TxDocked5AM.JoinNumber + i]);
                    Txs[i].TxStatusFeedback
                        .LinkInputSig(trilist.StringInput[joinMap.TxStatusText.JoinNumber + i]);
                    Txs[i].PercentChargeFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.PercentCharge.JoinNumber + i]);
                    Txs[i].PercentHealthFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.PercentHealth.JoinNumber + i]);
                }

                // device information feedback
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
            DeviceFirmwareVersionFeedback.FireUpdate();

            for (ushort i = 0; i < Mxwapxd2Size; i++)
            {
                Txs[i].TxEnabledFeedback.FireUpdate();
                Txs[i].TxPresentFeedback.FireUpdate();
                Txs[i].TxPresent5AMFeedback.FireUpdate();
                Txs[i].TxStatusFeedback.FireUpdate();
                Txs[i].PercentChargeFeedback.FireUpdate();
                Txs[i].PercentHealthFeedback.FireUpdate();
            }

            TxCheckRan5AMFeedback.FireUpdate();
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

    public class ShureMxwTx
    {
        #region Tx Enabled

        private bool _TxEnabled;

        public bool TxEnabled
        {
            get { return _TxEnabled; }
            set
            {
                _TxEnabled = value;
                TxEnabledFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Tx enabled feedback
        /// </summary>
        public BoolFeedback TxEnabledFeedback { get; private set; }

        #endregion

        #region Tx Present

        private bool _TxPresent;

        public bool TxPresent
        {
            get { return _TxPresent; }
            set
            {
                _TxPresent = value;
                TxPresentFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Tx present feedback
        /// </summary>
        public BoolFeedback TxPresentFeedback { get; private set; }

        #endregion

        #region Tx Present 5AM

        private bool _TxPresent5AM;

        public bool TxPresent5AM
        {
            get { return _TxPresent5AM; }
            set
            {
                _TxPresent5AM = value;
                TxPresent5AMFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Tx present 5AM feedback
        /// </summary>
        public BoolFeedback TxPresent5AMFeedback { get; private set; }

        #endregion

        #region Tx State (TX_STATUS)

        private string _TxStatus;

        public string TxStatus
        {
            get { return _TxStatus; }
            set
            {
                _TxStatus = value;
                TxPresent = (value.Length > 0 && value == "ON_CHARGER");
                TxStatusFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Tx state message feedback
        /// </summary>
        public StringFeedback TxStatusFeedback { get; private set; }

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

        public ShureMxwTx()
        {
            TxEnabledFeedback = new BoolFeedback(() => TxEnabled);
            TxPresentFeedback = new BoolFeedback(() => TxPresent);
            TxPresent5AMFeedback = new BoolFeedback(() => TxPresent5AM);
            TxStatusFeedback = new StringFeedback(() => TxStatus);
            PercentChargeFeedback = new IntFeedback(() => PercentCharge);
            PercentHealthFeedback = new IntFeedback(() => PercentHealth);
        }
    }

    public class ShureMxwapxd2BridgeJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        /// <summary>
        /// Get device online feedback
        /// </summary>
        [JoinName("IsOnline")] public readonly JoinDataComplete IsOnline = new JoinDataComplete(
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
        [JoinName("RefreshData")] public readonly JoinDataComplete RefreshData = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh all tx data",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get enabled feedback for a tx
        /// </summary>
        [JoinName("TxEnabled")] public readonly JoinDataComplete TxEnabled = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Enabled feedback for a tx",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get docked feedback for a tx
        /// </summary>
        [JoinName("TxDocked")] public readonly JoinDataComplete TxDocked = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Docked feedback for a tx",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get docked feedback for a tx at 5AM
        /// </summary>
        [JoinName("TxDocked5AM")] public readonly JoinDataComplete TxDocked5AM = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 31,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Docked feedback at 5AM for a tx",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Report docked check ran at 5AM
        /// </summary>
        [JoinName("Docked5AMCheckRan")] public readonly JoinDataComplete Docked5AMCheckRan = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 40,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Report if tx docked check at 5AM ran already",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion

        #region Analog

        /// <summary>
        /// Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        [JoinName("SocketStatus")] public readonly JoinDataComplete SocketStatus = new JoinDataComplete(
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
        [JoinName("MonitorStatus")] public readonly JoinDataComplete MonitorStatus = new JoinDataComplete(
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
        [JoinName("PercentCharge")] public readonly JoinDataComplete PercentCharge = new JoinDataComplete(
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
        [JoinName("PercentHealth")] public readonly JoinDataComplete PercentHealth = new JoinDataComplete(
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

        #endregion

        #region Serial

        /// <summary>
        /// Get the device name
        /// </summary>
        [JoinName("DeviceName")] public readonly JoinDataComplete DeviceName = new JoinDataComplete(
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
        /// Get the device model
        /// </summary>
        [JoinName("DeviceModel")] public readonly JoinDataComplete DeviceModel = new JoinDataComplete(
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
        /// Get the device firmware version
        /// </summary>
        [JoinName("DeviceFirmwareVersion")] public readonly JoinDataComplete DeviceFirmwareVersion =
            new JoinDataComplete(
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
        /// Get the tx status text
        /// </summary>
        [JoinName("TxStatusText")] public readonly JoinDataComplete TxStatusText = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                Description = "Tx Status Text",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        /// <summary>
        /// Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public ShureMxwapxd2BridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(ShureMxwapxd2BridgeJoinMap))
        {
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
            TypeNames = new List<string>() { "shureMxwapxd2" };
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

                // build the device comms (for all other comms methods) & check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new ShureMxwapxd2Device(dc.Key, dc.Name, comms);
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