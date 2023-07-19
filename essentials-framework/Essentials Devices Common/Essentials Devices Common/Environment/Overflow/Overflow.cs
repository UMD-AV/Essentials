using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;

namespace OverflowPlugin
{
    public class Overflow : EssentialsBridgeableDevice
    {
        private ThreeSeriesTcpIpEthernetIntersystemCommunications OverflowEisc;
        private BasicTriList InternalEisc;
        private BoolFeedback OverflowOnline;
        private BoolFeedback InternalOnline;
        private BoolFeedback RemoteOverflowOn;
        private BoolFeedback RemoteOverflowOff;
        private OverflowBridgeJoinMap overflowJoinMap = new OverflowBridgeJoinMap(1);

        private uint internalJoinOffset;
        private uint endInternalJoin;

        public Overflow(string key, string name, OverflowPropertiesConfig props)
            : base(key, name)
        {
            OverflowEisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(props.Control.IpIdInt, props.Control.TcpSshProperties.Address, Global.ControlSystem);
            OverflowOnline = new BoolFeedback(() => OverflowEisc.IsOnline);
            OverflowEisc.SigChange += new SigEventHandler(OverflowEisc_SigChange);
            OverflowEisc.OnlineStatusChange += new Crestron.SimplSharpPro.OnlineStatusChangeEventHandler(OverflowEisc_OnlineStatusChange);
            RemoteOverflowOn = new BoolFeedback(() => OverflowEisc.BooleanInput[overflowJoinMap.OverflowOn.JoinNumber].BoolValue);
            RemoteOverflowOff = new BoolFeedback(() => OverflowEisc.BooleanInput[overflowJoinMap.OverflowOff.JoinNumber].BoolValue);
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new OverflowBridgeJoinMap(joinStart);
            internalJoinOffset = joinStart - 1;
            endInternalJoin = joinStart + 10;
            InternalEisc = trilist;
            InternalOnline = new BoolFeedback(() => trilist.IsOnline);
            trilist.SigChange += new SigEventHandler(InternalEisc_SigChange);
            trilist.OnlineStatusChange += new OnlineStatusChangeEventHandler(InternalEisc_OnlineStatusChange);

            //Send this device name to SIMPL
            InternalEisc.StringInput[joinMap.DeviceName.JoinNumber].StringValue = this.Name;

            //Send camera EISC online status to SIMPL on join 1
            OverflowOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            RemoteOverflowOn.LinkInputSig(trilist.BooleanInput[joinMap.OverflowOn.JoinNumber]);
            RemoteOverflowOff.LinkInputSig(trilist.BooleanInput[joinMap.OverflowOff.JoinNumber]);
        }

        public override bool CustomActivate()
        {
            OverflowEisc.Register();
            RemoteOverflowOn.FireUpdate();
            RemoteOverflowOff.FireUpdate();
            return true;
        }

        private void OverflowEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Overflow Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID, args.Sig.Type, args.Sig.Number);

            switch (args.Sig.Type)
            {
                case eSigType.Bool :
                {
                    //Remote overflow command
                    if (args.Sig.Number == overflowJoinMap.OverflowOn.JoinNumber)
                    {
                        RemoteOverflowOn.FireUpdate();
                    }
                    else if (args.Sig.Number == overflowJoinMap.OverflowOff.JoinNumber)
                    {
                        RemoteOverflowOff.FireUpdate();
                    }
                    break;
                }
                case eSigType.UShort:
                {
                    break;
                }
                case eSigType.String:
                {
                    break;
                }
            }
        }


        private void InternalEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Internal Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID, args.Sig.Type, args.Sig.Number);

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    {
                        //For sending commands to remote overflow - shift to joins 1-10 on remote EISC
                        if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin && OverflowEisc != null)
                        {
                            OverflowEisc.BooleanInput[args.Sig.Number - internalJoinOffset].BoolValue = args.Sig.BoolValue;
                        }
                        break;
                    }
                case eSigType.UShort:
                    {
                        //For sending commands to remote overflow - shift to joins 1-10 on remote EISC
                        if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin && OverflowEisc != null)
                        {
                            OverflowEisc.UShortInput[args.Sig.Number - internalJoinOffset].UShortValue = args.Sig.UShortValue;
                        }
                        break;
                    }
                case eSigType.String:
                    {
                        //For sending commands to remote overflow - shift to joins 1-10 on remote EISC
                        if (args.Sig.Number > internalJoinOffset && args.Sig.Number <= endInternalJoin && OverflowEisc != null)
                        {
                            OverflowEisc.StringInput[args.Sig.Number - internalJoinOffset].StringValue = args.Sig.StringValue;
                        }
                        break;
                    }
            }
        }

        private void OverflowEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            OverflowOnline.FireUpdate();
        }

        private void InternalEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            InternalOnline.FireUpdate();
        }
    }

    public class OverflowPropertiesConfig
    {
        [JsonProperty("control")]
        public ControlPropertiesConfig Control { get; set; }
    }

    public class OverflowFactory : EssentialsDeviceFactory<Overflow>
    {
        public OverflowFactory()
        {
            TypeNames = new List<string>() { "overflow" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Overflow Device");
            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<OverflowPropertiesConfig>(dc.Properties.ToString());

            return new Overflow(dc.Key, dc.Name, props);
        }
    }

    public class OverflowBridgeJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Is Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OverflowOn")]
        public JoinDataComplete OverflowOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Overflow On",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OverflowOff")]
        public JoinDataComplete OverflowOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Overflow Off",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion

        #region Serial

        [JoinName("DeviceName")]
        public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });
        #endregion

        public OverflowBridgeJoinMap(uint joinStart)
            : base(joinStart, typeof(OverflowBridgeJoinMap))
        {
        }
    }

}