using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.Core.Shades;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    /// <summary>
    /// Controls a single shade using three relays
    /// </summary>
    public class RelayControlledShade : EssentialsBridgeableDevice, IShadesOpenCloseStop
    {
        RelayControlledShadeConfigProperties Config;

        GenericRelayDevice OpenShadesRelay;
        GenericRelayDevice StopShadesRelay;
        GenericRelayDevice CloseShadesRelay;

        public RelayControlledShade(string key, string name, RelayControlledShadeConfigProperties config)
            : base(key, name)
        {
            Config = config;
        }

        public override bool CustomActivate()
        {
            //Create ISwitchedOutput objects based on props
            OpenShadesRelay = DeviceManager.GetDeviceForKey(Config.OpenRelay) as GenericRelayDevice;
            if (Config.StopRelay != null)
            {
                StopShadesRelay = DeviceManager.GetDeviceForKey(Config.StopRelay) as GenericRelayDevice;
            }
            CloseShadesRelay = DeviceManager.GetDeviceForKey(Config.CloseRelay) as GenericRelayDevice;

            return base.CustomActivate();
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GenericShadesJoinMap(joinStart);
 
            trilist.StringInput[joinMap.ShadesOpenName.JoinNumber].StringValue = OpenShadesRelay.Name;
            trilist.StringInput[joinMap.ShadesCloseName.JoinNumber].StringValue = CloseShadesRelay.Name;
            if (Config.StopLabel != null)
            {
                trilist.StringInput[joinMap.ShadesStopName.JoinNumber].StringValue = Config.StopLabel;
            }
            else
            {
                trilist.StringInput[joinMap.ShadesStopName.JoinNumber].StringValue = StopShadesRelay.Name;
            }

            trilist.SetSigTrueAction(joinMap.ShadesOpen.JoinNumber, Open);
            trilist.SetSigTrueAction(joinMap.ShadesClose.JoinNumber, Close);
            trilist.SetSigTrueAction(joinMap.ShadesStop.JoinNumber, Stop);
        }

        public void Open()
        {
            Debug.Console(1, this, "Opening Shade: '{0}'", this.Name);
            PulseOutput(OpenShadesRelay, OpenShadesRelay.RelayHoldTimeSeconds * 1000);
        }

        public void Stop()
        {
            Debug.Console(1, this, "Stopping Shade: '{0}'", this.Name);
            if (Config.UseOpenCloseForStop)
            {
                PulseOutput(OpenShadesRelay, OpenShadesRelay.RelayHoldTimeSeconds * 1000);
                PulseOutput(CloseShadesRelay, CloseShadesRelay.RelayHoldTimeSeconds * 1000);
            }
            else if (StopShadesRelay != null)
            {
                PulseOutput(StopShadesRelay, StopShadesRelay.RelayHoldTimeSeconds * 1000);
            }
        }

        public void Close()
        {
            Debug.Console(1, this, "Closing Shade: '{0}'", this.Name);
            PulseOutput(CloseShadesRelay, CloseShadesRelay.RelayHoldTimeSeconds * 1000);
        }

        void PulseOutput(ISwitchedOutput output, int pulseTime)
        {
            output.On();
            CTimer pulseTimer = new CTimer(new CTimerCallbackFunction((o) => output.Off()), pulseTime);
        }
    }

    public class RelayControlledShadeConfigProperties
    {
        public string OpenRelay { get; set; }
        public string StopRelay { get; set; }
        public string CloseRelay { get; set; }
        public bool UseOpenCloseForStop { get; set; }
        public string StopLabel { get; set; }
    }

    public class RelayControlledShadeFactory : EssentialsDeviceFactory<RelayControlledShade>
    {
        public RelayControlledShadeFactory()
        {
            TypeNames = new List<string>() { "relaycontrolledshade" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Generic Comm Device");
            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<Environment.RelayControlledShadeConfigProperties>(dc.Properties.ToString());

            return new Environment.RelayControlledShade(dc.Key, dc.Name, props);
        }
    }

    public class GenericShadesJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("Shades Open")]
        public JoinDataComplete ShadesOpen = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Open", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Shades Close")]
        public JoinDataComplete ShadesClose = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Close", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Shades Stop")]
        public JoinDataComplete ShadesStop = new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Stop", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("Shades Open Name")]
        public JoinDataComplete ShadesOpenName = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Open Name", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Serial });

        [JoinName("Shades Close Name")]
        public JoinDataComplete ShadesCloseName = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Close Name", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Serial });

        [JoinName("Shades Stop Name")]
        public JoinDataComplete ShadesStopName = new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata { Description = "Shades Stop Name", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Serial });

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public GenericShadesJoinMap(uint joinStart)
            : this(joinStart, typeof(GenericShadesJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected GenericShadesJoinMap(uint joinStart, Type type)
            : base(joinStart, type)
        {
        }
    }
}