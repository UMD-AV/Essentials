using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;

using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;


using Newtonsoft.Json;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.Core.CrestronIO
{
    /// <summary>
    /// Represents a generic digital output deviced tied to a versiport
    /// </summary>
    public class GenericVersiportDigitalOutputDevice : EssentialsBridgeableDevice, ISwitchedOutput
    {
        public Versiport OutputPort { get; private set; }
        public ushort RelayHoldTimeSeconds { get; private set; }
        public BoolFeedback OutputIsOnFeedback { get; private set; }
        private CTimer OutputHoldTimer;

        Func<bool> OutputStateFeedbackFunc
        {
            get
            {
                return () => OutputPort.DigitalOut;
            }
        }

        public GenericVersiportDigitalOutputDevice(string key, string name, RelayPortConfig config) :
            base(key, name)
        {
            OutputIsOnFeedback = new BoolFeedback(OutputStateFeedbackFunc);
            OutputHoldTimer = new CTimer(OutputTimerCallback, Timeout.Infinite);
            OutputPort = GetVersiportDigitalOuput(config);
            OutputPort.Register();

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
                OutputPort.SetVersiportConfiguration(eVersiportConfiguration.DigitalOutput);
                OutputPort.VersiportChange += OutputPort_VersiportChange;
                Debug.Console(0, this, "Created GenericVersiportDigitalOutputDevice on port '{0}'.  Current State: {1}", config.PortNumber, OutputPort.DigitalOut);
            });
        }

        void OutputPort_VersiportChange(Versiport port, VersiportEventArgs args)
        {
            Debug.Console(1, this, "Versiport change: {0}", args.Event);

            if (args.Event == eVersiportEvent.DigitalInChange)
                OutputIsOnFeedback.FireUpdate();
        }

        #region Events

        void OutputTimerCallback(object o)
        {
            OutputPort.DigitalOut = false;
        }

        #endregion

        #region Methods

        public void PulseRelay()
        {
            OutputHoldTimer.Reset(RelayHoldTimeSeconds * 1000);
            OutputPort.DigitalOut = true;
        }

        public void StopPulse()
        {
            OpenOutput();
        }

        public void OpenOutput()
        {
            OutputHoldTimer.Reset(Timeout.Infinite);
            OutputPort.DigitalOut = false;
        }

        public void CloseOutput()
        {
            OutputPort.DigitalOut = true;
        }

        public void ToggleRelayState()
        {
            if (OutputPort.DigitalOut == true)
                OpenOutput();
            else
                CloseOutput();
        }

        #endregion

        #region ISwitchedOutput Members

        public void On()
        {
            CloseOutput();
        }

        public void Off()
        {
            OpenOutput();
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

            try
            {
                Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

                trilist.SetBoolSigAction(joinMap.Relay.JoinNumber, b =>
                {
                    if (b)
                        CloseOutput();
                    else
                        OpenOutput();
                });

                // feedback for output state
                OutputIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Relay.JoinNumber]);

                //feedback for name and relay time settings
                trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
                trilist.UShortInput[joinMap.RelayHoldTimeSeconds.JoinNumber].UShortValue = RelayHoldTimeSeconds;
            }
            catch (Exception e)
            {
                Debug.Console(1, this, "Unable to link device '{0}'.  Input is null", Key);
                Debug.Console(1, this, "Error: {0}", e);
            }
        }

        #endregion


        public static Versiport GetVersiportDigitalOuput(RelayPortConfig dc)
        {
            IIOPorts ioPortDevice;

            if (dc.PortDeviceKey.Equals("processor"))
            {
                if (!Global.ControlSystem.SupportsVersiport)
                {
                    Debug.Console(0, "GetVersiportDigitalOuput: Processor does not support Versiports");
                    return null;
                }
                ioPortDevice = Global.ControlSystem;
            }
            else
            {
                var ioPortDev = DeviceManager.GetDeviceForKey(dc.PortDeviceKey) as IIOPorts;
                if (ioPortDev == null)
                {
                    Debug.Console(0, "GetVersiportDigitalOuput: Device {0} is not a valid device", dc.PortDeviceKey);
                    return null;
                }
                ioPortDevice = ioPortDev;
            }
            if (ioPortDevice == null)
            {
                Debug.Console(0, "GetVersiportDigitalOuput: Device '0' is not a valid IIOPorts Device", dc.PortDeviceKey);
                return null;
            }

            if (dc.PortNumber > ioPortDevice.NumberOfVersiPorts)
            {
                Debug.Console(0, "GetVersiportDigitalOuput: Device {0} does not contain a port {1}", dc.PortDeviceKey, dc.PortNumber);
            }

            return ioPortDevice.VersiPorts[dc.PortNumber];
        }
    }

    public class GenericVersiportDigitalOutputDeviceFactory : EssentialsDeviceFactory<GenericVersiportDigitalOutputDevice>
    {
        public GenericVersiportDigitalOutputDeviceFactory()
        {
            TypeNames = new List<string>() { "versiportoutput" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Generic Versiport Output Device");

            var props = JsonConvert.DeserializeObject<RelayPortConfig>(dc.Properties.ToString());

            if (props == null) return null;

            var portDevice = new GenericVersiportDigitalOutputDevice(dc.Key, dc.Name, props);

            return portDevice;
        }
    }
}