using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Devices.Common.Catchbox
{
    public class CatchboxDevice : EssentialsBridgeableDevice
    {
        private readonly GenericUdpServer _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        public int CatchboxSize { get; private set; }
        public readonly CatchboxMicrophone[] Microphones;

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

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
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication"/>
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus"/>
        public CatchboxDevice(string key, string name, IBasicCommunication comms)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            CatchboxSize = 4;
            Microphones = new CatchboxMicrophone[4];
            for (ushort i = 0; i < 4; i++)
            {
                Microphones[i] = new CatchboxMicrophone();
                if (i < CatchboxSize)
                {
                    Microphones[i].MicrophoneEnabled = true;
                }
            }

            _comms = (GenericUdpServer)comms;
            if (_comms == null)
            {
                Debug.ConsoleWithLog(0, this, "Catchbox device must use udp as comm method");
                return;
            }

            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _comms.TextReceived += Handle_TextReceived;
            _comms.ConnectionChange += socket_ConnectionChange;
            SocketStatusFeedback = new IntFeedback(() => (int)_comms.ClientStatus);
        }

        /// <summary>
        /// Use custom activate to connect the device and start the comms monitor.
        /// This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            Debug.Console(0, this, "Connecting udp");
            _comms.Connect();
            UpdateStatus();
            _commsMonitor.Start();
            return base.CustomActivate();
        }

        // socket connection change event handler
        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.FireUpdate();
        }

        private void Handle_TextReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(0, this, "TextRecieved: {0}", args.Text);
        }

        /// <summary>
        /// Sends text to the device plugin comms
        /// </summary>
        /// <param name="text">Command to be sent</param>		
        public void SendText(string text)
        {
            if (_comms.IsConnected == false) return;

            if (string.IsNullOrEmpty(text)) return;

            Debug.Console(0, this, "SendText: {0}", text);
            _comms.SendText(text);
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
            SendText("{\"tx1\":{\"device\":{\"battery\":null}}}");
            SendText("{\"tx2\":{\"device\":{\"battery\":null}}}");
            SendText("{\"tx3\":{\"device\":{\"battery\":null}}}");
            SendText("{\"tx4\":{\"device\":{\"battery\":null}}}");
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
                CatchboxBridgeJoinMap joinMap = new CatchboxBridgeJoinMap(joinStart);

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

                // microphone info **feedback only**
                for (ushort i = 0; i < 4; i++)
                {
                    ushort index = i;
                    Microphones[index].MicrophoneEnabledFeedback
                        .LinkInputSig(trilist.BooleanInput[joinMap.MicrophoneEnabled.JoinNumber + index]);
                    Microphones[index].MicrophonePresentFeedback
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
            DeviceModelFeedback.FireUpdate();
            DeviceFirmwareVersionFeedback.FireUpdate();

            for (ushort i = 0; i < 4; i++)
            {
                Microphones[i].MicrophoneEnabledFeedback.FireUpdate();
                Microphones[i].MicrophonePresentFeedback.FireUpdate();
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
            SendText(CatchboxApi.GetDeviceVersion);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetDeviceType);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.SubscribeMic1);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetMic1Rssi);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetMic1Name);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetMic1LinkState);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetMic1BatteryLevel);
        }
    }

    public class CatchboxMicrophone
    {
        #region Microphone Enabled

        private bool _microphoneEnabled;

        public bool MicrophoneEnabled
        {
            get { return _microphoneEnabled; }
            set
            {
                _microphoneEnabled = value;
                MicrophoneEnabledFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Microphone enabled feedback
        /// </summary>
        public BoolFeedback MicrophoneEnabledFeedback { get; private set; }

        #endregion

        #region Microphone Present

        private bool _microphonePresent;

        public bool MicrophonePresent
        {
            get { return _microphonePresent; }
            set
            {
                _microphonePresent = value;
                MicrophonePresentFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Microphone present feedback
        /// </summary>
        public BoolFeedback MicrophonePresentFeedback { get; private set; }

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
                _percentHealth = value;
                PercentHealthFeedback.FireUpdate();
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

        #region Runtime (RUN_TIME)

        private int _runtime;

        public int Runtime
        {
            get { return _runtime; }
            set
            {
                _runtime = value;
                RuntimeFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Run time feedback
        /// </summary>
        public IntFeedback RuntimeFeedback { get; private set; }

        #endregion

        #region Model (MODEL)

        private string _model;

        public string Model
        {
            get { return _model; }
            set
            {
                _model = value;
                ModelFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// Model feedback
        /// </summary>
        public StringFeedback ModelFeedback { get; private set; }

        #endregion

        public CatchboxMicrophone()
        {
            _runtime = 65535;
            MicrophoneEnabledFeedback = new BoolFeedback(() => MicrophoneEnabled);
            MicrophonePresentFeedback = new BoolFeedback(() => MicrophonePresent);
            PercentChargeFeedback = new IntFeedback(() => PercentCharge);
            PercentHealthFeedback = new IntFeedback(() => PercentHealth);
            TemperatureFFeedback = new IntFeedback(() => TemperatureF);
            RuntimeFeedback = new IntFeedback(() => Runtime);
            ModelFeedback = new StringFeedback(() => Model);
        }
    }

    public static class CatchboxApi
    {
        public const string SubscribeMic1 =
            "{\"subscribe\":[{\"#\":{\"enable\":true,\"period_ms\":0},\"rx\":{\"device\":{\"mic1_link_state\":null}},\"tx1\":{\"device\":{\"name\":null,\"rssi\":null,\"battery\":null}}}]}\n";

        public const string GetDeviceVersion = "{\"rx\":{\"device\":{\"firmware_info\":null}}}";

        public const string GetDeviceType = "{\"rx\":{\"device\":{\"device_type\":null}}}";

        public const string GetMic1Rssi = "{\"tx1\":{\"device\":{\"rssi\":null}}}";
        public const string GetMic1Name = "{\"tx1\":{\"device\":{\"name\":null}}}";
        public const string GetMic1LinkState = "{\"rx\":{\"device\":{\"mic1_link_state\":null}}}";
        public const string GetMic1BatteryLevel = "{\"tx1\":{\"device\":{\"battery\":null}}}";
    }

    public class CatchboxBridgeJoinMap : JoinMapBaseAdvanced
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
        [JoinName("RefreshData")] public readonly JoinDataComplete RefreshData = new JoinDataComplete(
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
        [JoinName("MicrophoneEnabled")] public readonly JoinDataComplete MicrophoneEnabled = new JoinDataComplete(
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
        [JoinName("MicrophonePresent")] public readonly JoinDataComplete MicrophonePresent = new JoinDataComplete(
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
        [JoinName("PercentCharge")] public readonly JoinDataComplete PercentCharge = new JoinDataComplete(
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
        [JoinName("PercentHealth")] public readonly JoinDataComplete PercentHealth = new JoinDataComplete(
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
        [JoinName("TemperatureF")] public readonly JoinDataComplete TemperatureF = new JoinDataComplete(
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
        [JoinName("Runtime")] public readonly JoinDataComplete Runtime = new JoinDataComplete(
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
        /// Get the microphone model
        /// </summary>
        [JoinName("Model")] public readonly JoinDataComplete Model = new JoinDataComplete(
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
        public CatchboxBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(CatchboxBridgeJoinMap))
        {
        }
    }

    /// <summary>
    /// Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class CatchboxFactory : EssentialsDeviceFactory<CatchboxDevice>
    {
        /// <summary>
        /// Device factory constructor
        /// </summary>
        public CatchboxFactory()
        {
            // In the constructor, we initialize the list with the typenames that will build an instance of this device
            // only include unique typenames. When the constructor is used, all the typenames will be evaluated in lower case.
            TypeNames = new List<string>() { "catchbox" };
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

                // build the device comms (for all other comms methods) and check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new CatchboxDevice(dc.Key, dc.Name, comms);
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