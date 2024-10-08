using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.CrestronIO;

namespace PepperDash.Essentials.Devices.Common.Environment.Generic
{
    public class SerialControlledRelay : EssentialsBridgeableDevice
    {
        private readonly string _openCommand;
        private readonly string _closeCommand;
        public ushort RelayHoldTimeSeconds { get; private set; }
        private readonly CTimer RelayHoldTimer;
        public IBasicCommunication Communication { get; private set; }

        public SerialControlledRelay(string key, string name, IBasicCommunication comm,
            SerialControlledRelayConfig config)
            : base(key, name)
        {
            Communication = comm;
            _openCommand = config.OpenCommand;
            _closeCommand = config.CloseCommand;

            RelayHoldTimer = new CTimer(RelayTimerCallback, Timeout.Infinite);

            if (config.RelayHoldTimeSeconds >= 1)
            {
                RelayHoldTimeSeconds = config.RelayHoldTimeSeconds;
            }
            else
            {
                RelayHoldTimeSeconds = (ushort)1;
            }
        }

        #region Bridge Linking

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericRelayControllerJoinMap joinMap = new GenericRelayControllerJoinMap(joinStart);

            string joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<GenericRelayControllerJoinMap>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            trilist.SetBoolSigAction(joinMap.Relay.JoinNumber, b =>
            {
                if (b)
                    CloseRelay();
                else
                    OpenRelay();
            });

            //feedback for name and relay time settings
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
            trilist.UShortInput[joinMap.RelayHoldTimeSeconds.JoinNumber].UShortValue = RelayHoldTimeSeconds;
        }

        #endregion

        /// <summary>
        /// Sets the relay to pulse for the designated time
        /// </summary>
        /// 
        public void PulseRelay()
        {
            RelayHoldTimer.Reset(RelayHoldTimeSeconds * 1000);
            if (_closeCommand != null)
                Communication.SendText(_closeCommand);
        }

        /// <summary>
        /// Sets the relay to Close
        /// </summary>
        /// 
        public void CloseRelay()
        {
            if (_closeCommand != null)
                Communication.SendText(_closeCommand);
        }

        /// <summary>
        /// Sets the relay to Open
        /// </summary>
        /// 
        public void OpenRelay()
        {
            RelayHoldTimer.Reset(Timeout.Infinite);
            if (_openCommand != null)
                Communication.SendText(_openCommand);
        }

        private void RelayTimerCallback(object o)
        {
            if (_openCommand != null)
                Communication.SendText(_openCommand);
        }
    }

    public class SerialControlledLiftFactory : EssentialsDeviceFactory<SerialControlledRelay>
    {
        public SerialControlledLiftFactory()
        {
            TypeNames = new List<string>() { "seriallift" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Serial Controlled Lift Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            SerialControlledRelayConfig props =
                JsonConvert.DeserializeObject<SerialControlledRelayConfig>(dc.Properties.ToString());

            if (props == null) return null;

            return new SerialControlledRelay(dc.Key, dc.Name, comm, props);
        }
    }

    public class SerialControlledRelayConfig
    {
        [JsonProperty("openCommand")] public string OpenCommand { get; set; }
        [JsonProperty("closeCommand")] public string CloseCommand { get; set; }
        [JsonProperty("relayHoldTimeSeconds")] public ushort RelayHoldTimeSeconds { get; set; }
    }
}