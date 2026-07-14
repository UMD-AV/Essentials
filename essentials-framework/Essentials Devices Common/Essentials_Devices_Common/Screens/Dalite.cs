using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;
using UmdEssentials.Core.CrestronIO;

namespace UmdEssentials.Devices.Common.Screens
{
    /// <summary>
    /// Represents a dalite screen controlled by relays
    /// </summary>
    public class DaliteScreen : EssentialsBridgeableDevice, ISwitchedOutput
    {
        public Relay RelayOutput { get; private set; }
        public ushort RelayHoldTimeSeconds { get; private set; }
        private CTimer RelayHoldTimer;

        public BoolFeedback OutputIsOnFeedback { get; private set; }

        public DaliteScreen(string key, string name, Func<RelayPortConfig, Relay> postActivationFunc,
            RelayPortConfig config)
            : base(key, name)
        {
            OutputIsOnFeedback = new BoolFeedback(() => RelayOutput != null && RelayOutput.State);
            RelayHoldTimeSeconds = config.RelayHoldTimeSeconds >= 1 ? config.RelayHoldTimeSeconds : (ushort)1;

            AddPostActivationAction(() =>
            {
                RelayOutput = postActivationFunc(config);

                if (RelayOutput == null)
                {
                    Debug.Console(0, this, Debug.ErrorLogLevel.Error,
                        "Unable to get parent relay device for device key {0} and port {1}", config.PortDeviceKey,
                        config.PortNumber);
                    return;
                }

                RelayHoldTimer = new CTimer(RelayTimerCallback, Timeout.Infinite);
                RelayOutput.Register();

                RelayOutput.StateChange += RelayOutput_StateChange;
            });
        }

        #region Events

        private void RelayOutput_StateChange(Relay relay, RelayEventArgs args)
        {
            OutputIsOnFeedback.FireUpdate();
        }

        private void RelayTimerCallback(object o)
        {
            RelayOutput.State = false;
            Debug.Console(0, this, "Relay '{0}' open", Key);
        }

        #endregion

        #region Methods

        public void OpenRelay()
        {
            //Keep relay latched for additional 3s for dalite
            RelayHoldTimer.Reset(3000);
        }

        public void CloseRelay()
        {
            RelayHoldTimer.Reset(Timeout.Infinite);
            RelayOutput.State = true;
            Debug.Console(0, this, "Relay '{0}' closed", Key);
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
            GenericRelayControllerJoinMap joinMap = new GenericRelayControllerJoinMap(joinStart);

            string joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<GenericRelayControllerJoinMap>(joinMapSerialized);

            if (bridge != null)
                bridge.AddJoinMap(Key, joinMap);
            else
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");

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

        public class DaliteScreenDeviceFactory : EssentialsDeviceFactory<DaliteScreen>
        {
            public DaliteScreenDeviceFactory()
            {
                TypeNames = new List<string> { "dalitescreen" };
            }

            public override EssentialsDevice BuildDevice(DeviceConfig dc)
            {
                Debug.Console(1, "Factory Attempting to create new Dalite Screen Device");

                RelayPortConfig props = JsonConvert.DeserializeObject<RelayPortConfig>(dc.Properties.ToString());

                if (props == null) return null;

                DaliteScreen screen = new DaliteScreen(dc.Key, dc.Name, GenericRelayDevice.GetRelay, props);

                return screen;
            }
        }

        #endregion
    }
}