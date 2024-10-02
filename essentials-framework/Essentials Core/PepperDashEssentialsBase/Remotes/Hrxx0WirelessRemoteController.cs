using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.Remotes;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Essentials.Core
{
    [Description("Wrapper class for all HR-Series remotes")]
    public class Hrxx0WirelessRemoteController : EssentialsBridgeableDevice, IHasFeedback, IHR52Button
    {
        private CenRfgwController _gateway;

        private Hr1x0WirelessRemoteBase _remote;

        public FeedbackCollection<Feedback> Feedbacks { get; set; }

        public CrestronCollection<Button> Buttons
        {
            get { return _remote.Button; }
        }

        private DeviceConfig _config;

        public Hrxx0WirelessRemoteController(string key, Func<DeviceConfig, Hr1x0WirelessRemoteBase> preActivationFunc,
            DeviceConfig config)
            : base(key, config.Name)
        {
            Feedbacks = new FeedbackCollection<Feedback>();

            CrestronRemotePropertiesConfig props =
                JsonConvert.DeserializeObject<CrestronRemotePropertiesConfig>(config.Properties.ToString());

            _config = config;

            if (props.GatewayDeviceKey == "processor")
            {
                {
                    AddPreActivationAction(() =>
                    {
                        _remote = preActivationFunc(config);
                        RegisterEvents();
                    });

                    return;
                }
            }


            CenRfgwController gatewayDev = DeviceManager.GetDeviceForKey(props.GatewayDeviceKey) as CenRfgwController;
            if (gatewayDev == null)
            {
                Debug.Console(0, "GetHr1x0WirelessRemote: Device '{0}' is not a valid device", props.GatewayDeviceKey);
            }

            if (gatewayDev != null)
            {
                Debug.Console(0, "GetHr1x0WirelessRemote: Device '{0}' is a valid device", props.GatewayDeviceKey);
                _gateway = gatewayDev;
            }


            if (_gateway == null) return;

            _gateway.IsReadyEvent += _gateway_IsReadyEvent;
            if (_gateway.IsReady)
            {
                AddPreActivationAction(() =>
                {
                    _remote = preActivationFunc(config);

                    RegisterEvents();
                });
            }
        }

        private void _gateway_IsReadyEvent(object sender, IsReadyEventArgs e)
        {
            if (e.IsReady != true) return;
            _remote = GetHr1x0WirelessRemote(_config);

            RegisterEvents();
        }

        private void _remote_BaseEvent(GenericBase device, BaseEventArgs args)
        {
            if (args.EventId == Hr1x0EventIds.BatteryCriticalFeedbackEventId)
                Feedbacks["BatteryCritical"].FireUpdate();
            if (args.EventId == Hr1x0EventIds.BatteryLowFeedbackEventId)
                Feedbacks["BatteryLow"].FireUpdate();
            if (args.EventId == Hr1x0EventIds.BatteryVoltageFeedbackEventId)
                Feedbacks["BatteryVoltage"].FireUpdate();
        }

        private void RegisterEvents()
        {
            _remote.ButtonStateChange += _remote_ButtonStateChange;

            Feedbacks.Add(new BoolFeedback("BatteryCritical", () => _remote.BatteryCriticalFeedback.BoolValue));
            Feedbacks.Add(new BoolFeedback("BatteryLow", () => _remote.BatteryLowFeedback.BoolValue));
            Feedbacks.Add(new IntFeedback("BatteryVoltage", () => _remote.BatteryVoltageFeedback.UShortValue));

            _remote.BaseEvent += _remote_BaseEvent;
        }

        private void _remote_ButtonStateChange(GenericBase device, ButtonEventArgs args)
        {
            try
            {
                object handler = args.Button.UserObject;

                if (handler == null) return;

                Debug.Console(1, this, "Executing Action: {0}", handler.ToString());

                if (handler is Action<bool>)
                {
                    (handler as Action<bool>)(args.Button.State == eButtonState.Pressed ? true : false);
                }

                ButtonEventHandler newHandler = ButtonStateChange;
                if (ButtonStateChange != null)
                {
                    newHandler(device, args);
                }

                EssentialsButtonEventHandler newerHandler = EssentialsButtonStateChange;
                if (EssentialsButtonStateChange != null)
                {
                    newerHandler(this, args);
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Error in ButtonStateChange handler: {0}", e);
            }
        }


        #region Preactivation

        private static Hr1x0WirelessRemoteBase GetHr1x0WirelessRemote(DeviceConfig config)
        {
            CrestronRemotePropertiesConfig props =
                JsonConvert.DeserializeObject<CrestronRemotePropertiesConfig>(config.Properties.ToString());

            string type = config.Type;
            uint rfId = (uint)props.Control.InfinetIdInt;

            GatewayBase gateway;

            if (props.GatewayDeviceKey == "processor")
            {
                gateway = Global.ControlSystem.ControllerRFGatewayDevice;
            }
            else
            {
                CenRfgwController gatewayDev =
                    DeviceManager.GetDeviceForKey(props.GatewayDeviceKey) as CenRfgwController;
                if (gatewayDev == null)
                {
                    Debug.Console(0, "GetHr1x0WirelessRemote: Device '{0}' is not a valid device",
                        props.GatewayDeviceKey);
                    return null;
                }

                Debug.Console(0, "GetHr1x0WirelessRemote: Device '{0}' is a valid device", props.GatewayDeviceKey);
                gateway = gatewayDev.GateWay;
            }

            if (gateway == null)
            {
                Debug.Console(0, "GetHr1x0WirelessRemote: Device '{0}' is not a valid gateway", props.GatewayDeviceKey);
                return null;
            }

            Hr1x0WirelessRemoteBase remoteBase;
            switch (type)
            {
                case ("hr100"):
                    remoteBase = new Hr100(rfId, gateway);
                    break;
                case ("hr150"):
                    remoteBase = new Hr150(rfId, gateway);
                    break;
                case ("hr310"):
                    remoteBase = new Hr310(rfId, gateway);
                    break;
                default:
                    return null;
            }

            // register the device when using an internal RF gateway
            if (props.GatewayDeviceKey == "processor")
            {
                remoteBase.RegisterWithLogging(config.Key);
            }

            return remoteBase;
        }

        #endregion

        #region Factory

        public class Hrxx0WirelessRemoteControllerFactory : EssentialsDeviceFactory<Hrxx0WirelessRemoteController>
        {
            public Hrxx0WirelessRemoteControllerFactory()
            {
                TypeNames = new List<string>() { "hr100", "hr150", "hr310" };
            }

            public override EssentialsDevice BuildDevice(DeviceConfig dc)
            {
                Debug.Console(1, "Factory Attempting to create new HR-x00 Remote Device");

                return new Hrxx0WirelessRemoteController(dc.Key, GetHr1x0WirelessRemote, dc);
            }
        }

        #endregion

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            Hrxxx0WirelessRemoteControllerJoinMap joinMap = new Hrxxx0WirelessRemoteControllerJoinMap(joinStart);

            string joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<Hrxxx0WirelessRemoteControllerJoinMap>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            //List<string> ExcludedKeys = new List<string>();
            foreach (Feedback feedback in Feedbacks)
            {
                Feedback myFeedback = feedback;

                KeyValuePair<string, JoinDataComplete> joinData =
                    joinMap.Joins.FirstOrDefault(
                        x =>
                            x.Key.Equals(myFeedback.Key, StringComparison.InvariantCultureIgnoreCase));

                if (string.IsNullOrEmpty((joinData.Key))) continue;

                string name = joinData.Key;
                JoinDataComplete join = joinData.Value;

                if (join.Metadata.JoinType == eJoinType.Digital)
                {
                    Debug.Console(0, this, "Linking Bool Feedback '{0}' to join {1}", name, join.JoinNumber);
                    BoolFeedback someFeedback = myFeedback as BoolFeedback;
                    if (someFeedback == null) continue;
                    someFeedback.LinkInputSig(trilist.BooleanInput[join.JoinNumber]);
                }

                if (join.Metadata.JoinType == eJoinType.Analog)
                {
                    Debug.Console(0, this, "Linking Analog Feedback '{0}' to join {1}", name, join.JoinNumber);
                    IntFeedback someFeedback = myFeedback as IntFeedback;
                    if (someFeedback == null) continue;
                    someFeedback.LinkInputSig(trilist.UShortInput[join.JoinNumber]);
                }

                if (join.Metadata.JoinType == eJoinType.Serial)
                {
                    Debug.Console(0, this, "Linking Serial Feedback '{0}' to join {1}", name, join.JoinNumber);
                    StringFeedback someFeedback = myFeedback as StringFeedback;
                    if (someFeedback == null) continue;
                    someFeedback.LinkInputSig(trilist.StringInput[join.JoinNumber]);
                }
            }

            //var newJoinKeys = joinMap.Joins.Keys.Except(ExcludedKeys).ToList();

            //var newJoinMap = newJoinKeys.Where(k => joinMap.Joins.ContainsKey(k)).Select(k => joinMap.Joins[k]);


            Debug.Console(2, this, "There are {0} remote buttons", _remote.Button.Count);
            for (uint i = 1; i <= _remote.Button.Count; i++)
            {
                Debug.Console(2, this, "Attempting to link join index {0}", i);
                uint index = i;
                KeyValuePair<string, JoinDataComplete> joinData =
                    joinMap.Joins.FirstOrDefault(
                        o =>
                            o.Key.Equals(_remote.Button[index].Name.ToString(),
                                StringComparison.InvariantCultureIgnoreCase));

                if (string.IsNullOrEmpty((joinData.Key))) continue;

                JoinDataComplete join = joinData.Value;
                string name = joinData.Key;

                Debug.Console(2, this, "Setting User Object for '{0}'", name);
                if (join.Metadata.JoinType == eJoinType.Digital)
                {
                    _remote.Button[i].SetButtonAction((b) => trilist.BooleanInput[join.JoinNumber].BoolValue = b);
                }
            }

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;

                foreach (Feedback feedback in Feedbacks)
                {
                    feedback.FireUpdate();
                }
            };
        }

        public void SetTrilistBool(BasicTriList trilist, uint join, bool b)
        {
            trilist.BooleanInput[join].BoolValue = b;
        }


        #region IHR52Button Members

        public Button Custom9
        {
            get
            {
                IHR52Button localRemote = (IHR52Button)_remote;
                return localRemote == null ? null : localRemote.Custom9;
            }
        }

        public Button Favorite
        {
            get
            {
                IHR52Button localRemote = (IHR52Button)_remote;
                return localRemote == null ? null : localRemote.Favorite;
            }
        }


        public Button Home
        {
            get
            {
                IHR52Button localRemote = (IHR52Button)_remote;
                return localRemote == null ? null : localRemote.Home;
            }
        }

        #endregion

        #region IHR49Button Members

        public Button Clear
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Clear;
            }
        }

        public Button Custom5
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Custom5;
            }
        }

        public Button Custom6
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Custom6;
            }
        }

        public Button Custom7
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Custom7;
            }
        }

        public Button Custom8
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Custom8;
            }
        }

        public Button Enter
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Enter;
            }
        }

        public Button Keypad0
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad0;
            }
        }

        public Button Keypad1
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad1;
            }
        }

        public Button Keypad2Abc
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad2Abc;
            }
        }

        public Button Keypad3Def
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad3Def;
            }
        }

        public Button Keypad4Ghi
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad4Ghi;
            }
        }

        public Button Keypad5Jkl
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad5Jkl;
            }
        }

        public Button Keypad6Mno
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad6Mno;
            }
        }

        public Button Keypad7Pqrs
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad7Pqrs;
            }
        }

        public Button Keypad8Tuv
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad8Tuv;
            }
        }

        public Button Keypad9Wxyz
        {
            get
            {
                IHR49Button localRemote = (IHR49Button)_remote;
                return localRemote == null ? null : localRemote.Keypad9Wxyz;
            }
        }

        #endregion

        #region IHR33Button Members

        public Button Blue
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Blue;
            }
        }

        public Button ChannelDown
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.ChannelDown;
            }
        }

        public Button ChannelUp
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.ChannelUp;
            }
        }

        public Button Custom1
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Custom1;
            }
        }

        public Button Custom2
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Custom2;
            }
        }

        public Button Custom3
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Custom3;
            }
        }

        public Button Custom4
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Custom4;
            }
        }

        public Button DialPadDown
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.DialPadDown;
            }
        }

        public Button DialPadEnter
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.DialPadEnter;
            }
        }

        public Button DialPadLeft
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.DialPadLeft;
            }
        }

        public Button DialPadRight
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.DialPadRight;
            }
        }

        public Button DialPadUp
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.DialPadUp;
            }
        }

        public Button Dvr
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Dvr;
            }
        }

        public Button Exit
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Exit;
            }
        }

        public Button FastForward
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.FastForward;
            }
        }

        public Button Green
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Green;
            }
        }

        public Button Guide
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Blue;
            }
        }

        public Button Information
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Information;
            }
        }

        public Button Last
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Last;
            }
        }

        public Button Menu
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Menu;
            }
        }

        public Button Mute
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Mute;
            }
        }

        public Button NextTrack
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.NextTrack;
            }
        }

        public Button Pause
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Pause;
            }
        }

        public Button Play
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Play;
            }
        }

        public Button Power
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Power;
            }
        }

        public Button PreviousTrack
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.PreviousTrack;
            }
        }

        public Button Record
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Record;
            }
        }

        public Button Red
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Red;
            }
        }

        public Button Rewind
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Rewind;
            }
        }

        public Button Stop
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Stop;
            }
        }

        public Button VolumeDown
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.VolumeDown;
            }
        }

        public Button VolumeUp
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.VolumeUp;
            }
        }

        public Button Yellow
        {
            get
            {
                IHR33Button localRemote = (IHR33Button)_remote;
                return localRemote == null ? null : localRemote.Yellow;
            }
        }

        #endregion

        #region IButton Members

        public CrestronCollection<Button> Button
        {
            get { return Buttons; }
        }

        public event ButtonEventHandler ButtonStateChange;

        public delegate void EssentialsButtonEventHandler(EssentialsDevice device, ButtonEventArgs args);

        public event EssentialsButtonEventHandler EssentialsButtonStateChange;

        #endregion
    }
}