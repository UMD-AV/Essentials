using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash_Essentials_Core.Monitoring;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Devices.Common.Microphones
{
    public class CatchboxDevice : EssentialsBridgeableDevice, IDisposable
    {
        private readonly GenericUdpServer _comms;
        private readonly ManualCommunicationMonitor _commsMonitor;
        public readonly WirelessMic[] Microphones;
        private CTimer _pollTimer;
        private ushort _pollCount;

        /// <summary>
        ///     Device constructor
        /// </summary>
        /// <param name="key">device key</param>
        /// <param name="name">device name</param>
        /// <param name="comms">device communication as IBasicCommunication</param>
        /// <param name="config"></param>
        /// <see cref="PepperDash.Core.IBasicCommunication" />
        /// <seealso cref="Crestron.SimplSharp.CrestronSockets.SocketStatus" />
        public CatchboxDevice(string key, string name, IBasicCommunication comms, MicController config)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new {0} instance", name);
            DeviceModelFeedback = new StringFeedback(() => DeviceModel);
            DeviceFirmwareVersionFeedback = new StringFeedback(() => DeviceFirmwareVersion);
            CatchboxSize = 4;
            Microphones = new WirelessMic[config.MicKeys.Length];
            for (ushort i = 0; i < config.MicKeys.Length; i++)
            {
                Microphones[i] = new WirelessMic(config.MicKeys[i], config.MicKeys[i])
                {
                    Model = "Catchbox"
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

            _comms = (GenericUdpServer)comms;
            if (_comms == null)
            {
                Debug.ConsoleWithLog(0, this, "Catchbox device must use udp as comm method");
                return;
            }

            _commsMonitor = new ManualCommunicationMonitor(this, 70000, 180000);
            _commsMonitor.StatusChange += (sender, args) =>
            {
                foreach (WirelessMic mic in Microphones) mic.IsOnline = args.Status == MonitorStatus.IsOk;
            };
            _comms.TextReceived += Handle_TextReceived;
            _comms.UpdateConnectionStatus += socket_ConnectionChange;
            SocketStatusFeedback = new IntFeedback(() => (int)_comms.ClientStatus);
        }

        public int CatchboxSize { get; private set; }

        /// <summary>
        ///     Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback SocketStatusFeedback { get; private set; }

        public void Dispose()
        {
            if (_pollTimer != null) _pollTimer.Dispose();
        }

        /// <summary>
        ///     Use custom activate to connect the device and start the comms monitor.
        ///     This method will be called when the device is built.
        /// </summary>
        /// <returns></returns>
        public override bool CustomActivate()
        {
            Debug.Console(1, this, "Connecting udp");
            _comms.Connect();
            _pollTimer = new CTimer(o => Poll(), null, 0, 30000);
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
                Debug.Console(1, this, "Connected udp, subscribing now");
                Subscribe();
            }
        }

        private void Handle_TextReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "Textreceived: {0}", args.Text);
            _commsMonitor.SetOnlineStatus(true);
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

                if (response["subscribe"] != null) Debug.Console(2, this, "Catchbox subscription feedback: {0}", text);
                if (response["rx"] != null) ProcessRxFeedback(response["rx"] as JObject);
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
            if (rx["device"] != null)
            {
                JObject device = rx["device"] as JObject;
                string firmware = GetStringValue(device, "firmware_info");
                if (firmware != null)
                {
                    DeviceFirmwareVersion = firmware;
                    Debug.Console(1, this, "Catchbox feedback: hub firmware {0}", firmware);
                }

                string deviceType = GetStringValue(device, "device_type");
                if (deviceType != null)
                {
                    DeviceModel = deviceType;
                    if (deviceType == "dual") CatchboxSize = 2;
                    Debug.Console(1, this, "Catchbox feedback: device type {0}", deviceType);
                }

                for (int i = 1; i <= Microphones.Length; i++)
                {
                    int linkState;
                    if (!TryGetIntValue(device, string.Format("mic{0}_link_state", i), out linkState))
                        continue;

                    WirelessMic microphone = Microphones[i - 1];
                    microphone.LinkState = (LinkStates)linkState;
                    Debug.Console(1, this, "Catchbox feedback: mic {0} link state {1} ({2})", i, linkState,
                        microphone.State);
                }
            }
            else if (rx["audio"] != null)
            {
                JObject audio = rx["audio"] as JObject;
                if (audio == null) return;

                JObject input = audio["input"] as JObject;
                if (input == null)
                    return;

                for (int i = 1; i <= Microphones.Length; i++)
                {
                    JObject mic = input[string.Format("mic{0}", i)] as JObject;

                    if (mic == null)
                        continue;

                    int muteState;
                    if (!TryGetIntValue(mic, "mute", out muteState))
                        continue;

                    WirelessMic microphone = Microphones[i - 1];
                    microphone.MuteState = muteState == 1;
                    Debug.Console(1, this, "Catchbox feedback: mic {0} mute state {1}", i, muteState);
                }
            }
        }

        private void ProcessTxFeedback(int txNumber, JObject tx)
        {
            if (tx == null || txNumber < 1 || txNumber > Microphones.Length)
                return;

            JObject device = tx["device"] as JObject;
            if (device == null)
                return;

            WirelessMic microphone = Microphones[txNumber - 1];

            int battery;
            if (TryGetIntValue(device, "battery", out battery))
            {
                microphone.PercentCharge = battery;
                Debug.Console(1, this, "Catchbox feedback: tx {0} battery {1}%", txNumber, battery);
            }

            string channelName = GetStringValue(device, "name");
            if (channelName != null)
            {
                microphone.Name = channelName;
                Debug.Console(1, this, "Catchbox feedback: tx {0} channel name {1}", txNumber, channelName);
            }

            int rssi;
            if (TryGetIntValue(device, "rssi", out rssi))
                Debug.Console(1, this, "Catchbox feedback: tx {0} rssi {1}", txNumber, rssi);
        }

        private static string GetMicMuteString(int micNumber)
        {
            return "{\"input\":{\"mic" + micNumber + "\":{\"mute\":null}}}";
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

        /// <summary>
        ///     Sends text to the device comms
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

            Debug.Console(1, this, "SendText: {0}", text);
            _comms.SendText(text);
        }

        #region Polls

        /// <summary>
        ///     Polls the device
        /// </summary>
        public void Poll()
        {
            try
            {
                if (_pollCount >= 10)
                {
                    _pollCount = 0;
                    GetMicData();
                }
                else
                {
                    _pollCount++;
                }

                if (_commsMonitor.IsOnlineFeedback.BoolValue)
                    return;
                Subscribe();
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Exception in poll: {0}", e.Message);
            }
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

                //subscribe to mic mute
                SendText(CatchboxApi.SubscribeAudio("rx", GetMicMuteString(i)));
                CrestronEnvironment.Sleep(100);
            }

            GetMicData();
        }

        /// <summary>
        ///     Update status and subscribe
        /// </summary>
        private void GetMicData()
        {
            for (int i = 1; i <= CatchboxSize; i++)
            {
                string tx = string.Format("tx{0}", i);
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

                //Get mic mute
                SendText(CatchboxApi.GetAudioData("rx", GetMicMuteString(i)));
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
                MicControllerJoinMap joinMap = new MicControllerJoinMap(joinStart);

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
            MicControllerUtilities.FireMicrophoneFeedbacks(Microphones);
        }

        #endregion Overrides of EssentialsBridgeableDevice
    }

    public static class CatchboxApi
    {
        public const string DevicePoll =
            "{\"subscribe\":[{\"#\":{\"enable\":true,\"period_ms\":30000},\"rx\":{\"device\":{\"name\":null}}}]}";

        public const string GetDeviceVersion = "{\"rx\":{\"device\":{\"firmware_info\":null}}}";
        public const string GetDeviceType = "{\"rx\":{\"device\":{\"device_type\":null}}}";

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

        public static string SubscribeAudio(string device1, string device2)
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
                            new { audio = new Dictionary<string, object> { { device2, null } } }
                        }
                    }
                }
            };

            return JsonConvert.SerializeObject(jsonData);
        }

        public static string GetAudioData(string device1, string device2)
        {
            Dictionary<string, object> jsonData = new Dictionary<string, object>
            {
                {
                    string.Format("{0}", device1), new Dictionary<string, object>
                    {
                        { "audio", new Dictionary<string, object> { { device2, null } } }
                    }
                }
            };

            return JsonConvert.SerializeObject(jsonData);
        }
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
            TypeNames = new List<string> { "catchbox" };
        }

        /// <summary>
        ///     Builds and returns an instance of ShureMxaDevice
        /// </summary>
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            try
            {
                Debug.Console(0, "[{0}] Factory attempting to create new device from type: {1}", dc.Key, dc.Type);

                MicController properties = dc.Properties.ToObject<MicController>();

                // build the device comms (for all other comms methods) and check for null			
                IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
                if (comms != null) return new CatchboxDevice(dc.Key, dc.Name, comms, properties);
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