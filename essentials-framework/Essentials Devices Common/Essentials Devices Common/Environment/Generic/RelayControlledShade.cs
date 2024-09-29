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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    /// <summary>
    /// Controls a single shade using three relays
    /// </summary>
    public class RelayControlledShade : EssentialsBridgeableDevice, IShadesOpenCloseStop
    {
        RelayControlledShadeConfigProperties Config;

        List<GenericRelayDevice> OpenShadesRelays;
        List<GenericRelayDevice> StopShadesRelays;
        List<GenericRelayDevice> CloseShadesRelays;

        public RelayControlledShade(string key, string name, RelayControlledShadeConfigProperties config)
            : base(key, name)
        {
            Config = config;
        }

        public override bool CustomActivate()
        {
            OpenShadesRelays = new List<GenericRelayDevice>();
            StopShadesRelays = new List<GenericRelayDevice>();
            CloseShadesRelays = new List<GenericRelayDevice>();

            //Create ISwitchedOutput objects based on props
            foreach (string x in Config.OpenRelay)
            {
                GenericRelayDevice relay = DeviceManager.GetDeviceForKey(x) as GenericRelayDevice;
                if (relay != null)
                {
                    OpenShadesRelays.Add(relay);
                }
            }
            if (Config.StopRelay != null)
            {
                foreach (string x in Config.StopRelay)
                {
                    GenericRelayDevice relay = DeviceManager.GetDeviceForKey(x) as GenericRelayDevice;
                    if(relay != null)
                    {
                        StopShadesRelays.Add(relay);
                    }
                }
            }
            foreach (string x in Config.CloseRelay)
            {
                GenericRelayDevice relay = DeviceManager.GetDeviceForKey(x) as GenericRelayDevice;
                if (relay != null)
                {
                    CloseShadesRelays.Add(relay);
                }
            }

            return base.CustomActivate();
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GenericShadesJoinMap(joinStart);
 
            trilist.StringInput[joinMap.ShadesOpenName.JoinNumber].StringValue = OpenShadesRelays[0].Name;
            trilist.StringInput[joinMap.ShadesCloseName.JoinNumber].StringValue = CloseShadesRelays[0].Name;
            if (Config.StopLabel != null)
            {
                trilist.StringInput[joinMap.ShadesStopName.JoinNumber].StringValue = Config.StopLabel;
            }
            else if (StopShadesRelays.Count > 0)
            {
                trilist.StringInput[joinMap.ShadesStopName.JoinNumber].StringValue = StopShadesRelays[0].Name;
            }

            trilist.SetSigTrueAction(joinMap.ShadesOpen.JoinNumber, Open);
            trilist.SetSigTrueAction(joinMap.ShadesClose.JoinNumber, Close);
            trilist.SetSigTrueAction(joinMap.ShadesStop.JoinNumber, Stop);
        }

        public void Open()
        {
            Debug.Console(1, this, "Opening Shade: '{0}'", this.Name);
            //Stop close
            foreach (var relay in CloseShadesRelays)
            {
                relay.StopPulse();
            }
            //Stop stop
            if (StopShadesRelays.Count > 0)
            {
                foreach (var relay in StopShadesRelays)
                {
                    relay.StopPulse();
                }
            }
            foreach (var relay in OpenShadesRelays)
            {
                relay.PulseRelay();
            }
        }

        public void Stop()
        {
            Debug.Console(1, this, "Stopping Shade: '{0}'", this.Name);
            if (Config.UseOpenCloseForStop)
            {
                foreach (var relay in OpenShadesRelays)
                {
                    relay.PulseRelay();
                }
                foreach (var relay in CloseShadesRelays)
                {
                    relay.PulseRelay();
                }
            }
            else
            {
                foreach (var relay in OpenShadesRelays)
                {
                    relay.StopPulse();
                }
                foreach (var relay in CloseShadesRelays)
                {
                    relay.StopPulse();
                }
                if (StopShadesRelays.Count > 0)
                {
                    foreach (var relay in StopShadesRelays)
                    {
                        relay.PulseRelay();
                    }
                }
            }
        }

        public void Close()
        {
            Debug.Console(1, this, "Closing Shade: '{0}'", this.Name);
            //Stop open
            foreach (var relay in OpenShadesRelays)
            {
                relay.StopPulse();
            }
            //Stop stop
            if (StopShadesRelays.Count > 0)
            {
                foreach (var relay in StopShadesRelays)
                {
                    relay.StopPulse();
                }
            }
            foreach (var relay in CloseShadesRelays)
            {
                relay.PulseRelay();
            }
        }
    }

    public class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objecType)
        {
            return (objecType == typeof(List<T>));
        }

        public override object ReadJson(JsonReader reader, Type objecType, object existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>();
            }
            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class RelayControlledShadeConfigProperties
    {
        [JsonProperty("openRelay")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> OpenRelay { get; set; }

        [JsonProperty("stopRelay")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> StopRelay { get; set; }

        [JsonProperty("closeRelay")]
        [JsonConverter(typeof(SingleOrArrayConverter<string>))]
        public List<string> CloseRelay { get; set; }

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