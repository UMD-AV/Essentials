using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Devices.Common.Environment.CrestronLighting
{
    public class CrestronLighting : EssentialsBridgeableDevice, IDisposable
    {
        private ThreeSeriesTcpIpEthernetIntersystemCommunications LightingEisc;
        private BasicTriList InternalEisc;
        private BoolFeedback LightingOnline;
        private BoolFeedback InternalOnline;

        private const ushort startJoin = 10;
        private const ushort endJoin = 50;
        private uint internalJoinOffset;
        private uint internalStartJoin;
        private uint internalEndJoin;

        public CrestronLighting(string key, string name, CrestronLightingPropertiesConfig props)
            : base(key, name)
        {
            LightingEisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(props.Control.IpIdInt,
                props.Control.TcpSshProperties.Address, Global.ControlSystem);
            LightingOnline = new BoolFeedback(() => LightingEisc.IsOnline);
            LightingEisc.SigChange += LightingEisc_SigChange;
            LightingEisc.OnlineStatusChange +=
                LightingEisc_OnlineStatusChange;
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericLightingJoinMap joinMap = new GenericLightingJoinMap(joinStart);
            internalJoinOffset = joinStart - 1;
            internalStartJoin = startJoin + internalJoinOffset;
            internalEndJoin = endJoin + internalJoinOffset;

            InternalEisc = trilist;
            InternalOnline = new BoolFeedback(() => InternalEisc.IsOnline);
            InternalEisc.SigChange += InternalEisc_SigChange;
            InternalEisc.OnlineStatusChange += InternalEisc_OnlineStatusChange;

            //Send this device name to SIMPL
            InternalEisc.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

            //Send lighting EISC online status to SIMPL on join 1
            LightingOnline.LinkInputSig(InternalEisc.BooleanInput[joinMap.IsOnline.JoinNumber]);

            //Send internal EISC online status to lighting on join 1
            InternalOnline.LinkInputSig(LightingEisc.BooleanInput[joinMap.IsOnline.JoinNumber]);

            InternalOnline.FireUpdate();
            LightingOnline.FireUpdate();
        }

        public override bool CustomActivate()
        {
            LightingEisc.Register();
            return true;
        }

        private void LightingEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Lighting Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID,
                args.Sig.Type, args.Sig.Number);

            if (InternalEisc == null)
            {
                return;
            }

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                {
                    if (args.Sig.Number >= startJoin && args.Sig.Number <= endJoin)
                    {
                        InternalEisc.BooleanInput[args.Sig.Number + internalJoinOffset].BoolValue = args.Sig.BoolValue;
                    }

                    break;
                }
                case eSigType.UShort:
                {
                    if (args.Sig.Number >= startJoin && args.Sig.Number <= endJoin)
                    {
                        InternalEisc.UShortInput[args.Sig.Number + internalJoinOffset].UShortValue =
                            args.Sig.UShortValue;
                    }

                    break;
                }
                case eSigType.String:
                {
                    if (args.Sig.Number >= startJoin && args.Sig.Number <= endJoin)
                    {
                        InternalEisc.StringInput[args.Sig.Number + internalJoinOffset].StringValue =
                            args.Sig.StringValue;
                    }

                    break;
                }
            }
        }

        private void InternalEisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            Debug.Console(2, this, "Internal Eisc change IPID: {0} Type:{1} Number:{2}", currentDevice.ID,
                args.Sig.Type, args.Sig.Number);

            if (LightingEisc == null)
            {
                return;
            }

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                {
                    if (args.Sig.Number >= internalStartJoin && args.Sig.Number <= internalEndJoin &&
                        LightingEisc != null)
                    {
                        LightingEisc.BooleanInput[args.Sig.Number - internalJoinOffset].BoolValue = args.Sig.BoolValue;
                    }

                    break;
                }
                case eSigType.UShort:
                {
                    if (args.Sig.Number >= internalStartJoin && args.Sig.Number <= internalEndJoin &&
                        LightingEisc != null)
                    {
                        LightingEisc.UShortInput[args.Sig.Number - internalJoinOffset].UShortValue =
                            args.Sig.UShortValue;
                    }

                    break;
                }
                case eSigType.String:
                {
                    if (args.Sig.Number >= internalStartJoin && args.Sig.Number <= internalEndJoin &&
                        LightingEisc != null)
                    {
                        LightingEisc.StringInput[args.Sig.Number - internalJoinOffset].StringValue =
                            args.Sig.StringValue;
                    }

                    break;
                }
            }
        }

        private void PushLightingOutputData()
        {
            if (LightingEisc != null && InternalEisc != null)
            {
                for (uint x = startJoin; x <= endJoin; x++)
                {
                    LightingEisc.BooleanInput[x].BoolValue =
                        InternalEisc.BooleanOutput[x + internalJoinOffset].BoolValue;
                    LightingEisc.UShortInput[x].UShortValue =
                        InternalEisc.UShortOutput[x + internalJoinOffset].UShortValue;
                    LightingEisc.StringInput[x].StringValue =
                        InternalEisc.StringOutput[x + internalJoinOffset].StringValue;
                }
            }
        }

        private void PushInternalOutputData()
        {
            if (LightingEisc != null && InternalEisc != null)
            {
                for (uint x = startJoin; x <= endJoin; x++)
                {
                    InternalEisc.BooleanInput[x + internalJoinOffset].BoolValue =
                        LightingEisc.BooleanOutput[x].BoolValue;
                    InternalEisc.UShortInput[x + internalJoinOffset].UShortValue =
                        LightingEisc.UShortOutput[x].UShortValue;
                    InternalEisc.StringInput[x + internalJoinOffset].StringValue =
                        LightingEisc.StringOutput[x].StringValue;
                }
            }
        }

        private void ClearLightingOutputData()
        {
            for (uint x = startJoin; x <= endJoin; x++)
            {
                LightingEisc.BooleanInput[x].BoolValue = false;
            }
        }

        private void ClearInternalOutputData()
        {
            for (uint x = startJoin; x <= endJoin; x++)
            {
                InternalEisc.BooleanInput[x + internalJoinOffset].BoolValue = false;
            }
        }

        private void LightingEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            LightingOnline.FireUpdate();
            if (args.DeviceOnLine)
            {
                PushLightingOutputData();
                PushInternalOutputData();
            }
            else
            {
                ClearInternalOutputData();
            }
        }

        private void InternalEisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            InternalOnline.FireUpdate();
            if (args.DeviceOnLine)
            {
                PushLightingOutputData();
                PushInternalOutputData();
            }
            else
            {
                ClearLightingOutputData();
            }
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
            Debug.Console(0, "Disposing Crestron Lighting");
            Dispose();
            Debug.Console(0, "Disposing Crestron Lighting Complete");
        }

        public void Dispose()
        {
            if (LightingEisc != null) LightingEisc.Dispose();
            if (InternalEisc != null) InternalEisc.Dispose();
        }
    }

    public class CrestronLightingPropertiesConfig
    {
        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
    }

    public class CrestronLightingFactory : EssentialsDeviceFactory<CrestronLighting>
    {
        public CrestronLightingFactory()
        {
            TypeNames = new List<string>() { "crestronlighting" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Crestron Lighting Device");
            CrestronLightingPropertiesConfig props =
                JsonConvert.DeserializeObject<CrestronLightingPropertiesConfig>(
                    dc.Properties.ToString());

            return new CrestronLighting(dc.Key, dc.Name, props);
        }
    }
}