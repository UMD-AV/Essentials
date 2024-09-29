using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.Core.CrestronIO
{
    /// <summary>
    /// Represents a generic device controlled by relays
    /// </summary>
    [Description("Wrapper class for a Relay")]
    public class GenericRelayDevice : EssentialsBridgeableDevice, ISwitchedOutput
    {
        public Relay RelayOutput { get; private set; }
        public ushort RelayHoldTimeSeconds { get; private set; }
        private CTimer RelayHoldTimer;

        public BoolFeedback OutputIsOnFeedback { get; private set; }

        //Maintained for compatibility with PepperDash.Essentials.Core.Devices.CrestronProcessor
        public GenericRelayDevice(string key, Relay relay) :
            base(key)
        {
            OutputIsOnFeedback = new BoolFeedback(new Func<bool>(() => RelayOutput.State));

            RelayOutput = relay;
            RelayHoldTimer = new CTimer(RelayTimerCallback, Timeout.Infinite);
            RelayOutput.Register();

            RelayOutput.StateChange += RelayOutput_StateChange;
        }

        public GenericRelayDevice(string key, string name, Func<RelayPortConfig, Relay> postActivationFunc,
            RelayPortConfig config)
            : base(key, name)
        {
            OutputIsOnFeedback = new BoolFeedback(() => RelayOutput.State);
            if (config.RelayHoldTimeSeconds >= 1)
            {
                RelayHoldTimeSeconds = config.RelayHoldTimeSeconds;
            }
            else
            {
                RelayHoldTimeSeconds = (ushort)1;
            }

            AddPostActivationAction(() =>
            {
                RelayOutput = postActivationFunc(config);

                if (RelayOutput == null)
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error, "Unable to get parent relay device for device key {0} and port {1}", config.PortDeviceKey, config.PortNumber);
                    return;
                }

                RelayHoldTimer = new CTimer(RelayTimerCallback, Timeout.Infinite);
                RelayOutput.Register();

                RelayOutput.StateChange += RelayOutput_StateChange;
            });
        }

        #region PreActivate

        private static Relay GetRelay(RelayPortConfig dc)
        {
            IRelayPorts relayDevice;

            if(dc.PortDeviceKey.Equals("processor"))
            {
                if (!Global.ControlSystem.SupportsRelay)
                {
                    Debug.Console(0, "Processor does not support relays");
                    return null;
                }
                relayDevice = Global.ControlSystem;

                return relayDevice.RelayPorts[dc.PortNumber];
            }
            
            var essentialsDevice = DeviceManager.GetDeviceForKey(dc.PortDeviceKey);
            if (essentialsDevice == null)
            {
                Debug.Console(0, "Device {0} was not found in Device Manager. Check configuration or for errors with device.", dc.PortDeviceKey);
                return null;
            }

            relayDevice = essentialsDevice as IRelayPorts;
            
            if (relayDevice == null)
            {
                Debug.Console(0, "Device {0} is not a valid relay parent. Please check configuration.", dc.PortDeviceKey);
                return null;
            }

            if (dc.PortNumber <= relayDevice.NumberOfRelayPorts)
            {
                return relayDevice.RelayPorts[dc.PortNumber];
            }

            Debug.Console(0, "Device {0} does not contain a port {1}", dc.PortDeviceKey, dc.PortNumber);
            return null;
        }

        #endregion

        #region Events

        void RelayOutput_StateChange(Relay relay, RelayEventArgs args)
        {
            OutputIsOnFeedback.FireUpdate();
        }

        void RelayTimerCallback(object o)
        {
            RelayOutput.State = false;
        }

        #endregion

        #region Methods

        public void PulseRelay()
        {
            RelayHoldTimer.Reset(RelayHoldTimeSeconds * 1000);
            RelayOutput.State = true;
        }

        public void StopPulse()
        {
            OpenRelay();
        }

        public void OpenRelay()
        {
            RelayHoldTimer.Reset(Timeout.Infinite);
            RelayOutput.State = false;
        }

        public void CloseRelay()
        {
            RelayOutput.State = true;
        }

        public void ToggleRelayState()
        {
            if (RelayOutput.State == true)
                OpenRelay();
            else
                CloseRelay();
        }

        #endregion

        #region ISwitchedOutput Members

        public void On()
        {
            CloseRelay();
        }

        public void Off()
        {
            OpenRelay();
        }

        #endregion

        #region Bridge Linking

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GenericRelayControllerJoinMap(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<GenericRelayControllerJoinMap>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this, "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            if (RelayOutput == null)
            {
                Debug.Console(1, this, "Unable to link device '{0}'.  Relay is null", Key);
                return;
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            trilist.SetBoolSigAction(joinMap.Relay.JoinNumber, b =>
            {
                if (b)
                    CloseRelay();
                else
                    OpenRelay();
            });

            // feedback for relay state
            OutputIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Relay.JoinNumber]);

            //feedback for name and relay time settings
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
            trilist.UShortInput[joinMap.RelayHoldTimeSeconds.JoinNumber].UShortValue = RelayHoldTimeSeconds;
        }

        #endregion

        #region Factory

        public class GenericRelayDeviceFactory : EssentialsDeviceFactory<GenericRelayDevice>
        {
            public GenericRelayDeviceFactory()
            {
                TypeNames = new List<string>() { "relayoutput" };
            }

            public override EssentialsDevice BuildDevice(DeviceConfig dc)
            {
                Debug.Console(1, "Factory Attempting to create new Generic Relay Device");

                var props = JsonConvert.DeserializeObject<RelayPortConfig>(dc.Properties.ToString());

                if (props == null) return null;

                var portDevice = new GenericRelayDevice(dc.Key, dc.Name, GetRelay, props);

                return portDevice;
            }
        }

        #endregion


    }


}