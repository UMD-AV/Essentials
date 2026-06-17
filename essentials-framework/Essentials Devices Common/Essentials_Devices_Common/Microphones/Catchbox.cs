using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Devices.Common.Microphones
{
    public class CatchboxDevice : EssentialsBridgeableDevice
    {
        private readonly GenericUdpServer _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        public readonly CatchboxMicrophone[] Microphones;

        /// <summary>
        ///     Device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <see cref="PepperDash.Core.IBasicCommunication" />
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus" />
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
                if (i < CatchboxSize) Microphones[i].MicrophoneEnabled = true;
            }

            _comms = (GenericUdpServer)comms;
            if (_comms == null)
            {
                Debug.ConsoleWithLog(0, this, "Catchbox device must use udp as comm method");
                return;
            }

            _commsMonitor = new GenericCommunicationMonitor(this, _comms, 30000, 180000, 300000, Poll);
            _comms.TextReceived += Handle_TextReceived;
            _comms.UpdateConnectionStatus += socket_ConnectionChange;
            SocketStatusFeedback = new IntFeedback(() => (int)_comms.ClientStatus);
        }

        public int CatchboxSize { get; private set; }

        /// <summary>
        ///     Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        /// <summary>
        ///     Use custom activate to connect the device and start the comms monitor.
        ///     This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            Debug.Console(0, this, "Connecting udp");
            _comms.Connect();
            _commsMonitor.Start();
            return base.CustomActivate();
        }

        // socket connection change event handler
        private void socket_ConnectionChange(object sender, GenericUdpConnectedEventArgs args)
        {
            if (SocketStatusFeedback != null)
                SocketStatusFeedback.FireUpdate();
            if (args.Connected)
            {
                Debug.Console(0, this, "Connected udp, subscribing now");
                Subscribe();
            }
        }

        private void Handle_TextReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(0, this, "Textreceived: {0}", args.Text);
            ProcessResponse(args.Text);
        }

        private void ProcessResponse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string trimmed = text.Trim('\0', '\r', '\n', ' ', '\t');
            if (trimmed.Length == 0)
                return;

            string[] messages = trimmed.Split('\r', '\n');
            foreach (string t in messages)
            {
                string message = t.Trim('\0', '\r', '\n', ' ', '\t');
                if (message.Length > 0)
                    ProcessJsonMessage(message);
            }
        }

        private void ProcessJsonMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                JObject response = JObject.Parse(text);
                int error;
                if (TryGetIntValue(response, "error", out error) && error != 0)
                    Debug.Console(0, this, "Catchbox feedback error {0}: {1}", error, text);

                if (response["subscribe"] != null) Debug.Console(0, this, "Catchbox subscription feedback: {0}", text);

                ProcessRxFeedback(response["rx"] as JObject);

                for (int i = 1; i <= Microphones.Length; i++)
                    ProcessTxFeedback(i, response[string.Format("tx{0}", i)] as JObject);
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Error processing Catchbox feedback '{0}': {1}", text, ex.Message);
            }
        }

        private void ProcessRxFeedback(JObject rx)
        {
            if (rx == null)
                return;

            JObject device = rx["device"] as JObject;
            if (device == null)
                return;

            string firmware = GetStringValue(device, "firmware_info");
            if (firmware != null)
            {
                DeviceFirmwareVersion = firmware;
                Debug.Console(0, this, "Catchbox feedback: hub firmware {0}", firmware);
            }

            string deviceType = GetStringValue(device, "device_type");
            if (deviceType != null)
            {
                DeviceModel = deviceType;
                Debug.Console(0, this, "Catchbox feedback: device type {0}", deviceType);
            }

            for (int i = 1; i <= Microphones.Length; i++)
            {
                int linkState;
                if (!TryGetIntValue(device, string.Format("mic{0}_link_state", i), out linkState))
                    continue;

                CatchboxMicrophone microphone = Microphones[i - 1];
                microphone.LinkState = linkState;
                microphone.MicrophonePresent = linkState != 0;
                Debug.Console(0, this, "Catchbox feedback: mic {0} link state {1} ({2})", i, linkState,
                    GetLinkStateName(linkState));
            }
        }

        private void ProcessTxFeedback(int txNumber, JObject tx)
        {
            if (tx == null || txNumber < 1 || txNumber > Microphones.Length)
                return;

            JObject device = tx["device"] as JObject;
            if (device == null)
                return;

            CatchboxMicrophone microphone = Microphones[txNumber - 1];

            int battery;
            if (TryGetIntValue(device, "battery", out battery))
            {
                microphone.PercentCharge = battery;
                Debug.Console(0, this, "Catchbox feedback: tx {0} battery {1}%", txNumber, battery);
            }

            string channelName = GetStringValue(device, "name");
            if (channelName != null)
            {
                microphone.Model = channelName;
                Debug.Console(0, this, "Catchbox feedback: tx {0} channel name {1}", txNumber, channelName);
            }

            int rssi;
            if (TryGetIntValue(device, "rssi", out rssi))
            {
                microphone.Rssi = rssi;
                Debug.Console(0, this, "Catchbox feedback: tx {0} rssi {1}", txNumber, rssi);
            }
        }

        private static bool TryGetIntValue(JToken container, string propertyName, out int value)
        {
            value = 0;
            if (container == null)
                return false;

            JToken token = container[propertyName];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return false;

            try
            {
                value = Convert.ToInt32(token.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetStringValue(JToken container, string propertyName)
        {
            if (container == null)
                return null;

            JToken token = container[propertyName];
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
                return null;

            return token.ToString();
        }

        private static string GetLinkStateName(int linkState)
        {
            switch (linkState)
            {
                case 0:
                    return "Disconnected";
                case 1:
                    return "Connected";
                case 2:
                    return "Pairing";
                case 3:
                    return "Charging";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        ///     Sends text to the device plugin comms
        /// </summary>
        /// <param name="text">Command to be sent</param>
        public void SendText(string text)
        {
            if (!_comms.IsConnected)
            {
                Debug.Console(0, this, "Not connected, ignoring command");
                return;
            }

            if (string.IsNullOrEmpty(text)) return;

            Debug.Console(0, this, "SendText: {0}", text);
            _comms.SendText(text);
        }

        #region Polls

        /// <summary>
        ///     Polls the device
        /// </summary>
        public void Poll()
        {
            CrestronEnvironment.Sleep(100);

            if (_commsMonitor.IsOnlineFeedback.BoolValue)
                return;

            Subscribe();
        }

        #endregion Polls

        /// <summary>
        ///     Update status and subscribe
        /// </summary>
        public void Subscribe()
        {
            SendText(CatchboxApi.DevicePoll);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetDeviceVersion);
            CrestronEnvironment.Sleep(100);
            SendText(CatchboxApi.GetDeviceType);
            CrestronEnvironment.Sleep(100);
            //SendText(CatchboxApi.SetUsbModeMicrophone);
            //CrestronEnvironment.Sleep(100);

            for (int i = 1; i <= CatchboxSize; i++)
            {
                string tx = string.Format("tx{0}", i);
                //subscribe to battery levels
                SendText(CatchboxApi.SubscribeTxRx(tx, "battery"));
                CrestronEnvironment.Sleep(100);

                //subscribe to mic states
                SendText(CatchboxApi.SubscribeTxRx("rx", string.Format("mic{0}_link_state", i)));
                CrestronEnvironment.Sleep(100);

                //subscribe to mic names
                SendText(CatchboxApi.SubscribeTxRx(tx, "name"));
                CrestronEnvironment.Sleep(100);

                //subscribe to mic rssi
                SendText(CatchboxApi.SubscribeTxRx(tx, "rssi"));
                CrestronEnvironment.Sleep(100);

                //Get battery levels
                SendText(CatchboxApi.GetTxRxData(tx, "battery"));
                CrestronEnvironment.Sleep(100);

                //Get mic states
                SendText(CatchboxApi.GetTxRxData("rx", string.Format("mic{0}_link_state", i)));
                CrestronEnvironment.Sleep(100);

                //Get mic names
                SendText(CatchboxApi.GetTxRxData(tx, "name"));
                CrestronEnvironment.Sleep(100);

                //Get mic rssi
                SendText(CatchboxApi.GetTxRxData(tx, "rssi"));
                CrestronEnvironment.Sleep(100);
            }
        }

        #region Device Info

        // device model field
        private string _deviceModel;

        /// <summary>
        ///     Device model property
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
        ///     Device model feedback
        /// </summary>
        public StringFeedback DeviceModelFeedback { get; private set; }

        // device firmware version field
        private string _deviceFirmwareVersion;

        /// <summary>
        ///     Device firmware property
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
        ///     Device firmware version feedback
        /// </summary>
        public StringFeedback DeviceFirmwareVersionFeedback { get; private set; }

        #endregion

        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        ///     Links the plugin device to the EISC bridge
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
                if (bridge != null) bridge.AddJoinMap(Key, joinMap);

                Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
                Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

                // links to bridge
                trilist.StringInput[joinMap.DeviceName.JoinNumber].StringValue = Name;
                trilist.SetSigTrueAction(joinMap.RefreshData.JoinNumber, Subscribe);

                // _commsMonitor.IsOnlineFeedback is used to drive IsOnlineFb on the bridge
                _commsMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
                SocketStatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.SocketStatus.JoinNumber]);

                // microphone info **feedback only**
                for (ushort i = 0; i < 4; i++)
                {
                    // ReSharper disable once InlineTemporaryVariable - required due to loop
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
                    Microphones[index].LinkStateFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.LinkState.JoinNumber + index]);
                    Microphones[index].RssiFeedback
                        .LinkInputSig(trilist.UShortInput[joinMap.Rssi.JoinNumber + index]);
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
                Microphones[i].LinkStateFeedback.FireUpdate();
                Microphones[i].RssiFeedback.FireUpdate();
                Microphones[i].ModelFeedback.FireUpdate();
            }
        }

        #endregion Overrides of EssentialsBridgeableDevice
    }


    public class CatchboxMicrophone
    {
        public CatchboxMicrophone()
        {
            _runtime = 65535;
            MicrophoneEnabledFeedback = new BoolFeedback(() => MicrophoneEnabled);
            MicrophonePresentFeedback = new BoolFeedback(() => MicrophonePresent);
            PercentChargeFeedback = new IntFeedback(() => PercentCharge);
            PercentHealthFeedback = new IntFeedback(() => PercentHealth);
            TemperatureFFeedback = new IntFeedback(() => TemperatureF);
            RuntimeFeedback = new IntFeedback(() => Runtime);
            LinkStateFeedback = new IntFeedback(() => LinkState);
            RssiFeedback = new IntFeedback(() => Rssi);
            ModelFeedback = new StringFeedback(() => Model);
        }

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
        ///     Microphone enabled feedback
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
        ///     Microphone present feedback
        /// </summary>
        public BoolFeedback MicrophonePresentFeedback { get; private set; }

        #endregion

        #region Link State

        private int _linkState;

        public int LinkState
        {
            get { return _linkState; }
            set
            {
                _linkState = value;
                LinkStateFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     Microphone link state feedback
        /// </summary>
        public IntFeedback LinkStateFeedback { get; private set; }

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
        ///     Battery percent charge feedback
        /// </summary>
        public IntFeedback PercentChargeFeedback { get; private set; }

        #endregion

        #region Rssi

        private int _rssi;

        public int Rssi
        {
            get { return _rssi; }
            set
            {
                _rssi = value;
                RssiFeedback.FireUpdate();
            }
        }

        /// <summary>
        ///     RF signal level feedback
        /// </summary>
        public IntFeedback RssiFeedback { get; private set; }

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
        ///     Battery percent health feedback
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
        ///     Battery temperature in F feedback
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
        ///     Run time feedback
        /// </summary>
        public IntFeedback RuntimeFeedback { get; private set; }

        #endregion

        #region Model (Channel Name)

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
        ///     Channel name feedback
        /// </summary>
        public StringFeedback ModelFeedback { get; private set; }

        #endregion
    }

    public static class CatchboxApi
    {
        public const string DevicePoll =
            "{\"subscribe\":[{\"#\":{\"enable\":true,\"period_ms\":30000},\"rx\":{\"device\":{\"name\":null}}}]}";

        public const string GetDeviceVersion = "{\"rx\":{\"device\":{\"firmware_info\":null}}}";
        public const string GetDeviceType = "{\"rx\":{\"device\":{\"device_type\":null}}}";
        public const string SetUsbModeMicrophone = "{\"rx\":{\"device\":{\"usb_device_mode\":1}}}";

        public static string SubscribeTxRx(string device1, string device2)
        {
            var jsonData = new
            {
                subscribe = new[]
                {
                    new Dictionary<string, object>
                    {
                        { "#", new { enable = true, period_ms = 0 } },
                        {
                            device1,
                            new { device = new Dictionary<string, object> { { device2, null } } }
                        }
                    }
                }
            };

            return JsonConvert.SerializeObject(jsonData);
        }

        public static string GetTxRxData(string device1, string device2)
        {
            Dictionary<string, object> jsonData = new Dictionary<string, object>
            {
                {
                    string.Format("{0}", device1), new Dictionary<string, object>
                    {
                        { "device", new Dictionary<string, object> { { device2, null } } }
                    }
                }
            };

            return JsonConvert.SerializeObject(jsonData);
        }
    }

    public class CatchboxBridgeJoinMap : JoinMapBaseAdvanced
    {
        /// <summary>
        ///     Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public CatchboxBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(CatchboxBridgeJoinMap))
        {
        }

        #region Digital

        /// <summary>
        ///     Get device online feedback
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
        ///     Refresh all data
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
        ///     Get enabled feedback for a microphone
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
        ///     Get present feedback for a microphone
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
        ///     Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus" />
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
        ///     Get device monitor status join map
        /// </summary>
        /// <see cref="PepperDash.Essentials.Core.MonitorStatus" />
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
        ///     Get percent charge for a microphone
        /// </summary>
        /// <remarks>
        ///     000-100 = percent charge,
        ///     254 = error,
        ///     255 = unknown
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
        ///     Get health for a microphone
        /// </summary>
        /// <remarks>
        ///     000-100 = percent health,
        ///     254 = error,
        ///     255 = unknown
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
        ///     Get temperature in F for a microphone
        /// </summary>
        /// <remarks>
        ///     000-253 = temperature in F,
        ///     254 = error,
        ///     255 = unknown
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
        ///     Get microphone runtime
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

        /// <summary>
        ///     Get microphone link state
        /// </summary>
        /// <remarks>
        ///     0 = disconnected, 1 = connected, 2 = pairing, 3 = charging
        /// </remarks>
        [JoinName("LinkState")] public readonly JoinDataComplete LinkState = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 51,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Microphone Link State",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        ///     Get microphone RSSI
        /// </summary>
        [JoinName("Rssi")] public readonly JoinDataComplete Rssi = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 61,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Microphone RSSI",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion

        #region Serial

        /// <summary>
        ///     Get device name
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
        ///     Get the device model
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
        ///     Get the device firmware version
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
        ///     Get the microphone channel name
        /// </summary>
        [JoinName("Model")] public readonly JoinDataComplete Model = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 4
            },
            new JoinMetadata
            {
                Description = "Microphone Channel Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion
    }

    /// <summary>
    ///     Plugin factory for devices that require communications using IBasicCommunications or custom communication methods
    /// </summary>
    public class CatchboxFactory : EssentialsDeviceFactory<CatchboxDevice>
    {
        /// <summary>
        ///     Device factory constructor
        /// </summary>
        public CatchboxFactory()
        {
            // In the constructor, we initialize the list with the typenames that will build an instance of this device
            // only include unique typenames. When the constructor is used, all the typenames will be evaluated in lower case.
            TypeNames = new List<string> { "catchbox" };
        }

        /// <summary>
        ///     Builds and returns an instance of ShureMxaDevice
        /// </summary>
        /// <param name="dc">device configuration</param>
        /// <returns>plugin device or null</returns>
        /// <seealso cref="PepperDash.Core.eControlMethod" />
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