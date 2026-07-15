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
    public class ShureUlxdDevice : EssentialsBridgeableDevice, IDisposable
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private const string CommsDelimiter = ">";
        private readonly GenericQueue _commsQueue;
        public int UlxdSize { get; private set; }

        private readonly Regex _regexPattern = new Regex(
            @"< REP (?<Index>[0-9]\s)?(?<Command>.*\b) (?<State>\w+|\{.*\}) >",
            RegexOptions.IgnoreCase);

        private readonly CommunicationGather _commsGather;

        public readonly WirelessMic[] Microphones;

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        /// Reports monitor status feedback through the bridge
        /// Typically used for Fusion status reporting and system status LED's
        /// </summary>
        public IntFeedback MonitorStatusFeedback { get; private set; }

        public bool IsOnline
        {
            get { return _commsMonitor != null && _commsMonitor.Status == MonitorStatus.IsOk; }
        }

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
        public ShureUlxdDevice(string key, string name, MicController config, IBasicCommunication comms)
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
            UlxdSize = 4;
            Microphones = new WirelessMic[UlxdSize];
            ushort i = 0;
            while (i < config.MicKeys.Length)
            {
                Microphones[i] = new WirelessMic(config.MicKeys[i], string.Format("{0}-{1}", name, i + 1), false)
                {
                    Model = "Shure Tx",
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

            while (i < UlxdSize)
            {
                Microphones[i] = new WirelessMic(Key + "-wmic" + i + 1, string.Format("{0}-{1}", name, i + 1), false)
                {
                    Model = "Shure Tx",
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
                // device comms is IP **ELSE** device comms is RS232
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
                // Microphone type
                // TX: < GET x TX_TYPE >
                // RX: < REP x TX_TYPE model >
                case "TX_TYPE":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    if (index >= 0 && index < UlxdSize)
                    {
                        if (state.Length == 0 || state == "UNKN")
                        {
                            Microphones[index].Model = "";
                            Microphones[index].State = state;
                            Microphones[index].MicrophoneInUse = false;
                        }
                        else
                        {
                            Microphones[index].Model = state;
                            Microphones[index].State = state;
                            Microphones[index].MicrophoneInUse = true;
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
                    if (index >= 0 && index < UlxdSize)
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
                    if (index >= 0 && index < UlxdSize)
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
                    if (index >= 0 && index < UlxdSize)
                    {
                        short stateInt = Convert.ToInt16(state);
                        if (stateInt >= 0 && stateInt <= 253)
                            Microphones[index].TemperatureF = stateInt;
                        else
                            Microphones[index].TemperatureF = 0;
                    }

                    break;
                }

                // Battery run time
                // TX: < GET x BATT_RUN_TIME >
                // RX: < REP x BATT_RUN_TIME 00125 >
                case "BATT_RUN_TIME":
                {
                    int index = Convert.ToInt16(indexString) - 1;
                    ushort stateInt = Convert.ToUInt16(state);
                    if (index >= 0 && index < UlxdSize) Microphones[index].Runtime = stateInt;

                    break;
                }

                // Model Number
                // TX: "< GET MODEL >"
                // RX: "< REP MODEL {y} >"	// y is 32-char model number
                case "MODEL":
                {
                    DeviceModel = state;
                    if (state.StartsWith("ULXD4Q"))
                        UlxdSize = 4;
                    else if (state.StartsWith("ULXD4D"))
                        UlxdSize = 2;
                    else if (state.StartsWith("ULXD4"))
                        UlxdSize = 1;
                    else
                        UlxdSize = 4;

                    break;
                }
                // Firmware Version
                // TX: "< GET FW_VER >"
                // RX: "< REP FW_VER {y} >" // y is 18-char firmware version
                case "FW_VER":
                {
                    DeviceFirmwareVersion = state;
                    for (ushort i = 0; i < UlxdSize; i++) Microphones[i].DeviceFirmwareVersion = state;
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
            SendText("GET 0 TX_TYPE");
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
                ShureUlxdBridgeJoinMap joinMap = new ShureUlxdBridgeJoinMap(joinStart);

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

                // microphone info **feedback only**
                for (ushort i = 0; i < 4; i++)
                {
                    ushort index = i;
                    Microphones[index].MicrophoneInUseFeedback
                        .LinkInputSig(trilist.BooleanInput[joinMap.MicrophonePresent.JoinNumber + index]);
                    Microphones[index].PercentChargeFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.PercentCharge.JoinNumber + index]);
                    Microphones[index].PercentHealthFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.PercentHealth.JoinNumber + index]);
                    Microphones[index].TemperatureFFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.TemperatureF.JoinNumber + index]);
                    Microphones[index].RuntimeFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.Runtime.JoinNumber + index]);
                    Microphones[index].ModelFeedback
                        .LinkInputSig(trilist.StringInput[joinMap.Model.JoinNumber + index]);
                }

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

            for (ushort i = 0; i < 4; i++)
            {
                Microphones[i].MicrophoneInUseFeedback.FireUpdate();
                Microphones[i].PercentChargeFeedback.FireUpdate();
                Microphones[i].PercentHealthFeedback.FireUpdate();
                Microphones[i].TemperatureFFeedback.FireUpdate();
                Microphones[i].RuntimeFeedback.FireUpdate();
                Microphones[i].ModelFeedback.FireUpdate();
            }
        }

        #endregion Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Update status of all parameters
        /// Shure command string API recommends running this command on first power up
        /// </summary>
        public void UpdateStatus()
        {
            SendText("GET 0 FW");
        }

        public void Dispose()
        {
            // Unsubscribe from events
            ISocketStatus socket = _comms as ISocketStatus;
            if (socket != null) socket.ConnectionChange -= socket_ConnectionChange;

            _commsGather.LineReceived -= Handle_LineReceived;

            // Stop the communication monitor
            _commsMonitor.Stop();

            // Clear the communication queue
            _commsQueue.Dispose();

            // Clear feedbacks
            SocketStatusFeedback = null;
            MonitorStatusFeedback = null;
            DeviceModelFeedback = null;
            DeviceFirmwareVersionFeedback = null;

            Debug.Console(0, this, "Disposed ShureUlxdDevice resources.");
        }
    }

    public class ShureUlxdBridgeJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        /// <summary>
        /// Get device online feedback
        /// </summary>
        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
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
        [JoinName("RefreshData")] public JoinDataComplete RefreshData = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh all device data",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get enabled feedback for a microphone
        /// </summary>
        [JoinName("MicrophoneEnabled")] public JoinDataComplete MicrophoneEnabled = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Enabled feedback for a microphone",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Get present feedback for a microphone
        /// </summary>
        [JoinName("MicrophonePresent")] public JoinDataComplete MicrophonePresent = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Present feedback for a microphone",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion

        #region Analog

        /// <summary>
        /// Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        [JoinName("SocketStatus")] public JoinDataComplete SocketStatus = new JoinDataComplete(
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
        /// <see cref="Core.MonitorStatus"/>
        [JoinName("MonitorStatus")] public JoinDataComplete MonitorStatus = new JoinDataComplete(
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
        /// Get percent charge for a microphone
        /// </summary>
        /// <remarks>
        /// 000-100 = percent charge,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("PercentCharge")] public JoinDataComplete PercentCharge = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Percent charge for a microphone",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get health for a microphone
        /// </summary>
        /// <remarks>
        /// 000-100 = percent health,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("PercentHealth")] public JoinDataComplete PercentHealth = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Percent health for a microphone",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get temperature in F for a microphone
        /// </summary>
        /// <remarks>
        /// 000-253 = temperature in F,
        /// 254 = error,
        /// 255 = unknown
        /// </remarks>
        [JoinName("TemperatureF")] public JoinDataComplete TemperatureF = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 31,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Temperature in F for a microphone",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Get microphone runtime
        /// </summary>
        [JoinName("Runtime")] public JoinDataComplete Runtime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 41,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Microphone Runtime",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion

        #region Serial

        /// <summary>
        /// Get device name
        /// </summary>
        [JoinName("DeviceName")] public JoinDataComplete DeviceName = new JoinDataComplete(
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
        [JoinName("DeviceModel")] public JoinDataComplete DeviceModel = new JoinDataComplete(
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
        [JoinName("DeviceFirmwareVersion")] public JoinDataComplete DeviceFirmwareVersion = new JoinDataComplete(
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
        /// Get microphone model
        /// </summary>
        [JoinName("Model")] public JoinDataComplete Model = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Microphone Model",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        /// <summary>
        /// Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public ShureUlxdBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(ShureUlxdBridgeJoinMap))
        {
        }
    }

    /// <summary>
    /// Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class ShureUlxdFactory : EssentialsDeviceFactory<ShureUlxdDevice>
    {
        /// <summary>
        /// Device factory constructor
        /// </summary>
        public ShureUlxdFactory()
        {
            TypeNames = new List<string> { "shureulxd" };
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
                if (comms != null) return new ShureUlxdDevice(dc.Key, dc.Name, propertiesConfig, comms);
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