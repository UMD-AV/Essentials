// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Interfaces;
using PepperDash.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharpPro.Fusion;
using Crestron.SimplSharpPro;
using Crestron.SimplSharp.CrestronXml;
using Crestron.SimplSharp.CrestronXml.Serialization;
using DynFusion.Assets;
using Crestron.SimplSharp;
using PepperDash_Essentials_Core.Extensions;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PepperDash.Essentials.Devices.Common.ShureSbc;
using PepperDash.Essentials.Devices.Common.ShureUlxd;

namespace DynFusion
{
    public class DynFusionDevice : EssentialsBridgeableDevice, ILogStringsWithLevel, ILogStrings
    {
        public const ushort FusionJoinOffset = 49;

        //DynFusion Joins
        public string customResourceConfig { get; set; }

        public event EventHandler<EventArgs> RoomInformationUpdated;

        private DynFusionConfigObjectTemplate _Config;
        private Dictionary<uint, DynFusionDigitalAttribute> DigitalAttributesToFusion;
        private Dictionary<uint, DynFusionAnalogAttribute> AnalogAttributesToFusion;
        private Dictionary<uint, DynFusionSerialAttribute> SerialAttributesToFusion;
        private Dictionary<uint, DynFusionDigitalAttribute> DigitalAttributesFromFusion;
        private Dictionary<uint, DynFusionAnalogAttribute> AnalogAttributesFromFusion;
        private Dictionary<uint, DynFusionSerialAttribute> SerialAttributesFromFusion;
        private Dictionary<uint, StaticAsset> StaticAssets;
        private static DynFusionJoinMap JoinMapStatic;
        private List<DynFusionAssetOccupancySensor> OccSensors;

        public BoolFeedback FusionOnlineFeedback;
        public RoomInformation RoomInformation;

        public DynFusionDeviceUsage DeviceUsage;
        public DynFusionHelpRequest HelpRequest;
        public FusionRoom FusionSymbol;
        private CTimer ErrorLogTimer;
        private CTimer EiscOfflineTimer;
        private CTimer OnlineEventTimer;
        private string ErrorLogLastMessageSent;
        private bool _isInitialized;

        public DynFusionDevice(string key, string name, DynFusionConfigObjectTemplate config)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new DynFusionDevice instance");
            _Config = config;
            DigitalAttributesToFusion = new Dictionary<uint, DynFusionDigitalAttribute>();
            AnalogAttributesToFusion = new Dictionary<uint, DynFusionAnalogAttribute>();
            SerialAttributesToFusion = new Dictionary<uint, DynFusionSerialAttribute>();
            DigitalAttributesFromFusion = new Dictionary<uint, DynFusionDigitalAttribute>();
            AnalogAttributesFromFusion = new Dictionary<uint, DynFusionAnalogAttribute>();
            SerialAttributesFromFusion = new Dictionary<uint, DynFusionSerialAttribute>();
            StaticAssets = new Dictionary<uint, StaticAsset>();
            OccSensors = new List<DynFusionAssetOccupancySensor>();
            JoinMapStatic = new DynFusionJoinMap(1);
            Debug.Console(2, "Creating Fusion Symbol {0} {1}", _Config.control.IpId, Key);

            FusionSymbol = new FusionRoom(_Config.control.IpIdInt, Global.ControlSystem, Key,
                FusionUuid.GenerateUuid(key));

            HelpRequest = new DynFusionHelpRequest(FusionSymbol.Help);

            if (FusionSymbol.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                Debug.Console(0, this, "Failure to register Fusion Symbol");
            }

            FusionSymbol.ExtenderFusionRoomDataReservedSigs.Use();
        }

        public override bool CustomActivate()
        {
            Init();
            return true;
        }

        private void Init()
        {
            try
            {
                Debug.Console(0, this, "Initializing Fusion");
                // Online Status 
                FusionOnlineFeedback = new BoolFeedback(() => { return FusionSymbol.IsOnline; });
                FusionSymbol.OnlineStatusChange += new OnlineStatusChangeEventHandler(FusionSymbol_OnlineStatusChange);

                CrestronEnvironment.EthernetEventHandler +=
                    new EthernetEventHandler(CrestronEnvironment_EthernetEventHandler);
                OnlineEventTimer = new CTimer(OnlineTimerExpired, Timeout.Infinite); //30 second timer

                // Attribute State Changes 
                FusionSymbol.FusionStateChange += new FusionStateEventHandler(FusionSymbol_FusionStateChange);
                FusionSymbol.ExtenderFusionRoomDataReservedSigs.DeviceExtenderSigChange +=
                    new DeviceExtenderJoinChangeEventHandler(FusionSymbol_RoomDataDeviceExtenderSigChange);

                if (customResourceConfig != null)
                {
                    try
                    {
                        DynFusionConfigObjectTemplate customAttrConfig = JObject.Parse(customResourceConfig)
                            .ToObject<DynFusionConfigObjectTemplate>();
                        Debug.Console(0, "Fusion embdedded config read");

                        // Create Custom Atributes 
                        if (customAttrConfig.CustomAttributes.DigitalAttributes != null)
                        {
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.DigitalAttributes)
                            {
                                Debug.Console(0, "Fusion embdedded attribute: {0}", att.Name);
                                FusionSymbol.AddSig(eSigType.Bool, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIOMask(att.RwType));

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    DigitalAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.Name, att.JoinNumber, att.LinkDeviceKey,
                                            att.LinkDeviceMethod, att.LinkDeviceFeedback));
                                    DigitalAttributesToFusion[att.JoinNumber].BoolValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedBooleanSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                {
                                    DigitalAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.Name, att.JoinNumber));
                                }
                            }
                        }

                        if (customAttrConfig.CustomAttributes.AnalogAttributes != null)
                        {
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.AnalogAttributes)
                            {
                                FusionSymbol.AddSig(eSigType.UShort, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIOMask(att.RwType));

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    AnalogAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.Name, att.JoinNumber));
                                    AnalogAttributesToFusion[att.JoinNumber].UShortValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedUShortSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                {
                                    AnalogAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.Name, att.JoinNumber));
                                }
                            }
                        }

                        if (customAttrConfig.CustomAttributes.SerialAttributes != null)
                        {
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.SerialAttributes)
                            {
                                FusionSymbol.AddSig(eSigType.String, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIOMask(att.RwType));
                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    SerialAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.Name, att.JoinNumber));
                                    SerialAttributesToFusion[att.JoinNumber].StringValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedStringSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                {
                                    SerialAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.Name, att.JoinNumber));
                                }
                            }
                        }


                        if (customAttrConfig.CustomProperties != null)
                        {
                            if (customAttrConfig.CustomProperties.DigitalProperties != null)
                            {
                                foreach (FusionCustomProperty att in
                                         customAttrConfig.CustomProperties.DigitalProperties)
                                {
                                    DigitalAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.ID, att.JoinNumber));
                                }
                            }

                            if (customAttrConfig.CustomProperties.AnalogProperties != null)
                            {
                                foreach (FusionCustomProperty att in customAttrConfig.CustomProperties.AnalogProperties)
                                {
                                    AnalogAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.ID, att.JoinNumber));
                                }
                            }

                            if (customAttrConfig.CustomProperties.SerialProperties != null)
                            {
                                foreach (FusionCustomProperty att in customAttrConfig.CustomProperties.SerialProperties)
                                {
                                    SerialAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.ID, att.JoinNumber));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, "Fusion embedded config exception: {0}", ex.Message);
                    }
                }

                // Create Links for Standard joins 
                CreateStandardJoin(JoinMapStatic.SystemPowerOn, FusionSymbol.SystemPowerOn);
                CreateStandardJoin(JoinMapStatic.SystemPowerOff, FusionSymbol.SystemPowerOff);
                CreateStandardJoin(JoinMapStatic.DisplayPowerOn, FusionSymbol.DisplayPowerOn);
                CreateStandardJoin(JoinMapStatic.DisplayPowerOff, FusionSymbol.DisplayPowerOff);
                CreateStandardJoin(JoinMapStatic.MsgBroadcastEnabled, FusionSymbol.MessageBroadcastEnabled);
                CreateStandardJoin(JoinMapStatic.AuthenticationSucceeded, FusionSymbol.AuthenticateSucceeded);
                CreateStandardJoin(JoinMapStatic.AuthenticationFailed, FusionSymbol.AuthenticateFailed);

                CreateStandardJoin(JoinMapStatic.DeviceUsage, FusionSymbol.DisplayUsage);
                CreateStandardJoin(JoinMapStatic.BroadcastMsgType, FusionSymbol.BroadcastMessageType);

                CreateStandardJoin(JoinMapStatic.ErrorMsg, FusionSymbol.ErrorMessage);
                CreateStandardJoin(JoinMapStatic.LogText, FusionSymbol.LogText);

                // Room Data Extender 
                CreateStandardJoin(JoinMapStatic.ActionQuery,
                    FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQuery);
                CreateStandardJoin(JoinMapStatic.RoomConfig,
                    FusionSymbol.ExtenderFusionRoomDataReservedSigs.RoomConfigQuery);

                HelpRequest.GetOpenItems();
                DeviceUsageFactory();

                //Static Assets
                FusionSymbol.FusionAssetStateChange += FusionSymbol_FusionAssetStateChange;

                try
                {
                    string occSensorName = "Room Occupancy";
                    uint tempAssetNumber = GetNextAvailableAssetNumber(FusionSymbol);
                    Debug.Console(2, this,
                        string.Format("Creating occSensor: {0}, {1}", tempAssetNumber, occSensorName));
                    FusionSymbol.AddAsset(eAssetType.OccupancySensor, tempAssetNumber, occSensorName,
                        "Occupancy Sensor", FusionUuid.GenerateUuid(occSensorName));
                    OccSensors.Add(new DynFusionAssetOccupancySensor(Key + "-" + occSensorName, 951, FusionSymbol,
                        tempAssetNumber));
                }
                catch (Exception ex)
                {
                    Debug.ConsoleWithLog(0, this, string.Format("Creating occSensor failed: {0}", ex.Message));
                }

                foreach (IKeyed device in DeviceManager.AllDevices)
                {
                    try
                    {
                        DisplayBase displayDevice = device as DisplayBase;
                        if (displayDevice != null)
                        {
                            uint num = GetNextAvailableAssetNumber(FusionSymbol);
                            StaticAssets.Add(num, new DisplayStaticAsset(displayDevice, num, FusionSymbol));
                            continue;
                        }

                        ShureSbcDevice sbcDevice =
                            device as PepperDash.Essentials.Devices.Common.ShureSbc.ShureSbcDevice;
                        if (sbcDevice != null)
                        {
                            for (uint i = 1; i <= sbcDevice.SbcSize; i++)
                            {
                                uint num = GetNextAvailableAssetNumber(FusionSymbol);
                                string name = string.Format("{0} - Battery {1}", sbcDevice.Name, i);
                                StaticAssets.Add(num,
                                    new MicBatteryStaticAsset(name, sbcDevice.Batteries[i - 1], num, FusionSymbol));
                            }

                            continue;
                        }

                        ShureUlxdDevice ulxdDevice =
                            device as PepperDash.Essentials.Devices.Common.ShureUlxd.ShureUlxdDevice;
                        if (ulxdDevice != null)
                        {
                            for (uint i = 1; i <= ulxdDevice.UlxdSize; i++)
                            {
                                uint num = GetNextAvailableAssetNumber(FusionSymbol);
                                string name = string.Format("{0} - Microphone {1}", ulxdDevice.Name, i);
                                StaticAssets.Add(num,
                                    new MicStaticAsset(name, ulxdDevice.Microphones[i - 1], num, FusionSymbol));
                            }

                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Exception creating static asset for device key {0}: {1}", device.Key,
                            ex.Message);
                    }
                }

                Debug.Console(0, this, "Generating Fuson RVI");
                FusionRVI.GenerateFileForAllFusionDevices();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "Exception DynFusion Initialize {0}", ex);
            }
        }

        void DeviceUsageFactory()
        {
            if (_Config.DeviceUsage != null)
            {
                DeviceUsage = new DynFusionDeviceUsage(string.Format("{0}-DeviceUsage", Key), this);
                if (_Config.DeviceUsage.usageMinThreshold > 0)
                {
                    DeviceUsage.usageMinThreshold = (int)_Config.DeviceUsage.usageMinThreshold;
                }

                if (_Config.DeviceUsage.Devices != null && _Config.DeviceUsage.Devices.Count > 0)
                {
                    foreach (DeviceUsageDevice device in _Config.DeviceUsage.Devices)
                    {
                        try
                        {
                            Debug.Console(1, this, "Creating Device: {0}, {1}, {2}", device.joinNumber, device.type,
                                device.name);
                            DeviceUsage.CreateDevice(device.joinNumber, device.type, device.name);
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "{0}", ex);
                        }
                    }
                }

                if (_Config.DeviceUsage.Displays != null && _Config.DeviceUsage.Displays.Count > 0)
                {
                    foreach (DisplayUsageDevice display in _Config.DeviceUsage.Displays)
                    {
                        try
                        {
                            Debug.Console(1, this, "Creating Display: {0}, {1}", display.joinNumber, display.name);
                            DeviceUsage.CreateDisplay(display.joinNumber, display.name);
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "{0}", ex);
                        }
                    }
                }

                if (_Config.DeviceUsage.Sources != null && _Config.DeviceUsage.Sources.Count > 0)
                {
                    foreach (DeviceUsageSoruce source in _Config.DeviceUsage.Sources)
                    {
                        try
                        {
                            Debug.Console(1, this, "Creating Source: {0}, {1}", source.sourceNumber, source.name);
                            DeviceUsage.CreateSource(source.sourceNumber, source.name, source.type);
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "{0}", ex);
                        }
                    }
                }
            }
        }

        void CreateStandardJoin(JoinDataComplete join, BooleanSigDataFixedName Sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
            {
                DigitalAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionDigitalAttribute(join.Metadata.Description, join.JoinNumber));
            }

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                DigitalAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionDigitalAttribute(join.Metadata.Description, join.JoinNumber));
                DigitalAttributesToFusion[join.JoinNumber].BoolValueFeedback.LinkInputSig(Sig.InputSig);
            }
        }

        void CreateStandardJoin(JoinDataComplete join, UShortSigDataFixedName Sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
            {
                AnalogAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionAnalogAttribute(join.Metadata.Description, join.JoinNumber));
            }

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                AnalogAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionAnalogAttribute(join.Metadata.Description, join.JoinNumber));
                AnalogAttributesToFusion[join.JoinNumber].UShortValueFeedback.LinkInputSig(Sig.InputSig);
            }
        }

        void CreateStandardJoin(JoinDataComplete join, StringSigDataFixedName Sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
            {
                SerialAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
            }

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                SerialAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
                SerialAttributesToFusion[join.JoinNumber].StringValueFeedback.LinkInputSig(Sig.InputSig);
            }
        }

        void CreateStandardJoin(JoinDataComplete join, StringInputSig Sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
            {
                SerialAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
            }

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                SerialAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
                SerialAttributesToFusion[join.JoinNumber].StringValueFeedback.LinkInputSig(Sig);
            }
        }

        void FusionSymbol_RoomDataDeviceExtenderSigChange(DeviceExtender currentDeviceExtender, SigEventArgs args)
        {
            Debug.Console(2, this,
                string.Format("DynFusion DeviceExtenderChange {0} {1} {2} {3}", currentDeviceExtender.ToString(),
                    args.Sig.Number, args.Sig.Type, args.Sig.StringValue));
            ushort joinNumber = (ushort)args.Sig.Number;

            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    break;

                case eSigType.UShort:
                    break;

                case eSigType.String:
                    DynFusionSerialAttribute output;

                    if (SerialAttributesFromFusion.TryGetValue(args.Sig.Number, out output))
                    {
                        output.StringValue = args.Sig.StringValue;
                    }

                    if (args.Sig == FusionSymbol.ExtenderFusionRoomDataReservedSigs.RoomConfigResponse &&
                        args.Sig.StringValue != null)
                    {
                        RoomConfigParseData(args.Sig.StringValue);
                    }

                    break;
            }
        }

        void FusionSymbol_FusionStateChange(FusionBase device, FusionStateEventArgs args)
        {
            Debug.Console(2, this, "DynFusion FusionStateChange {0} {1}", args.EventId,
                args.UserConfiguredSigDetail.ToString());
            switch (args.EventId)
            {
                case FusionEventIds.SystemPowerOnReceivedEventId:
                {
                    // Comments
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (DigitalAttributesFromFusion.TryGetValue(JoinMapStatic.SystemPowerOn.JoinNumber, out output))
                    {
                        output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }
                case FusionEventIds.SystemPowerOffReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (DigitalAttributesFromFusion.TryGetValue(JoinMapStatic.SystemPowerOff.JoinNumber, out output))
                    {
                        output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }
                case FusionEventIds.DisplayPowerOnReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (DigitalAttributesFromFusion.TryGetValue(JoinMapStatic.DisplayPowerOn.JoinNumber, out output))
                    {
                        output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }
                case FusionEventIds.DisplayPowerOffReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (DigitalAttributesFromFusion.TryGetValue(JoinMapStatic.DisplayPowerOff.JoinNumber, out output))
                    {
                        output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }
                case FusionEventIds.BroadcastMessageTypeReceivedEventId:
                {
                    UShortSigDataFixedName sigDetails = args.UserConfiguredSigDetail as UShortSigDataFixedName;
                    DynFusionAnalogAttribute output;
                    if (AnalogAttributesFromFusion.TryGetValue(JoinMapStatic.BroadcastMsgType.JoinNumber, out output))
                    {
                        output.UShortValue = sigDetails.OutputSig.UShortValue;
                    }

                    break;
                }
                case FusionEventIds.HelpMessageReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    Debug.Console(0, "Help Message: {0}", sigDetails.OutputSig.StringValue);
                    HelpRequest.ParseFeedback(sigDetails.OutputSig.StringValue);
                    break;
                }
                case FusionEventIds.TextMessageFromRoomReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (SerialAttributesFromFusion.TryGetValue(JoinMapStatic.TextMessage.JoinNumber, out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
                case FusionEventIds.BroadcastMessageReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (SerialAttributesFromFusion.TryGetValue(JoinMapStatic.BroadcastMsg.JoinNumber, out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
                case FusionEventIds.GroupMembershipRequestReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (SerialAttributesFromFusion.TryGetValue(JoinMapStatic.GroupMembership.JoinNumber, out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
                case FusionEventIds.AuthenticateFailedReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (SerialAttributesFromFusion.TryGetValue(JoinMapStatic.AuthenticationFailed.JoinNumber,
                            out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
                case FusionEventIds.AuthenticateSucceededReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (SerialAttributesFromFusion.TryGetValue(JoinMapStatic.AuthenticationSucceeded.JoinNumber,
                            out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
                case FusionEventIds.UserConfiguredBoolSigChangeEventId:
                {
                    BooleanSigData sigDetails = args.UserConfiguredSigDetail as BooleanSigData;
                    uint joinNumber = (uint)(sigDetails.Number + FusionJoinOffset);
                    DynFusionDigitalAttribute output;
                    Debug.Console(2, this, "DynFusion UserAttribute Digital Join:{0} Name:{1} Value:{2}", joinNumber,
                        sigDetails.Name, sigDetails.OutputSig.BoolValue);

                    if (DigitalAttributesFromFusion.TryGetValue(joinNumber, out output))
                    {
                        output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }

                case FusionEventIds.UserConfiguredUShortSigChangeEventId:
                {
                    UShortSigData sigDetails = args.UserConfiguredSigDetail as UShortSigData;
                    uint joinNumber = (uint)(sigDetails.Number + FusionJoinOffset);
                    DynFusionAnalogAttribute output;
                    Debug.Console(2, this, "DynFusion UserAttribute Analog Join:{0} Name:{1} Value:{2}", joinNumber,
                        sigDetails.Name, sigDetails.OutputSig.UShortValue);

                    if (AnalogAttributesFromFusion.TryGetValue(joinNumber, out output))
                    {
                        output.UShortValue = sigDetails.OutputSig.UShortValue;
                    }

                    break;
                }
                case FusionEventIds.UserConfiguredStringSigChangeEventId:
                {
                    StringSigData sigDetails = args.UserConfiguredSigDetail as StringSigData;
                    uint joinNumber = (uint)(sigDetails.Number + FusionJoinOffset);
                    DynFusionSerialAttribute output;
                    Debug.Console(2, this, "DynFusion UserAttribute Analog Join:{0} Name:{1} Value:{2}", joinNumber,
                        sigDetails.Name, sigDetails.OutputSig.StringValue);

                    if (SerialAttributesFromFusion.TryGetValue(joinNumber, out output))
                    {
                        output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
            }
        }

        void FusionSymbol_FusionAssetStateChange(FusionBase device, FusionAssetStateEventArgs args)
        {
            Debug.Console(1, this, "DynFusion Asset State Change index:{0}", args.UserConfigurableAssetDetailIndex);
            if (StaticAssets.ContainsKey(args.UserConfigurableAssetDetailIndex))
            {
                StaticAssets[args.UserConfigurableAssetDetailIndex].FusionAssetStateChange(args);
            }
        }

        void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs args)
        {
            if (_isInitialized && args.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
            {
                if (args.EthernetEventType == eEthernetEventType.LinkUp)
                {
                    Debug.Console(0, this, "Ethernet Link Up");
                    FusionSymbol.Register();
                }
                else if (args.EthernetEventType == eEthernetEventType.LinkDown)
                {
                    Debug.Console(0, this, "Ethernet Link Down");
                    FusionSymbol.UnRegister();
                }
            }
        }

        void FusionSymbol_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            FusionOnlineFeedback.FireUpdate();
            if (args.DeviceOnLine)
            {
                Debug.ConsoleWithLog(0, this, "DynFusion Symbol Online");
                OnlineEventTimer.Reset(5000);
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "DynFusion Symbol Offline");
                OnlineEventTimer.Stop();
            }
        }

        private void OnlineTimerExpired(object o)
        {
            if (FusionSymbol.IsOnline)
            {
                GetRoomConfig();
                HelpRequest.GetOpenItems();
                CrestronEnvironment.Sleep(60000);
                int count = 0;
                while (RoomInformation == null || RoomInformation.Name.Length < 1)
                {
                    Debug.ConsoleWithLog(0, this, "Room config not populated, retrying now");
                    GetRoomConfig();
                    CrestronEnvironment.Sleep(600000);
                    count++;
                    if (count > 10)
                        break;
                }
            }
        }

        private static eSigIoMask GetIOMask(eReadWrite mask)
        {
            eSigIoMask type = eSigIoMask.NA;

            switch (mask)
            {
                case eReadWrite.R:
                    type = eSigIoMask.InputSigOnly;
                    break;
                case eReadWrite.W:
                    type = eSigIoMask.OutputSigOnly;
                    break;
                case eReadWrite.RW:
                    type = eSigIoMask.InputOutputSig;
                    break;
            }

            return (type);
        }

        private static eSigIoMask GetIOMask(string mask)
        {
            eSigIoMask _RWType = eSigIoMask.NA;

            switch (mask)
            {
                case "R":
                    _RWType = eSigIoMask.InputSigOnly;
                    break;
                case "W":
                    _RWType = eSigIoMask.OutputSigOnly;
                    break;
                case "RW":
                    _RWType = eSigIoMask.InputOutputSig;
                    break;
            }

            return (_RWType);
        }

        private static eReadWrite GeteReadWrite(eJoinCapabilities mask)
        {
            eReadWrite type = eReadWrite.ReadWrite;

            switch (mask)
            {
                case eJoinCapabilities.FromSIMPL:
                    type = eReadWrite.Read;
                    break;
                case eJoinCapabilities.ToSIMPL:
                    type = eReadWrite.Write;
                    break;
                case eJoinCapabilities.ToFromSIMPL:
                    type = eReadWrite.ReadWrite;
                    break;
            }

            return (type);
        }

        public static uint GetNextAvailableAssetNumber(FusionRoom room)
        {
            uint slotNum = 1;
            foreach (CustomFusionAssetData item in room.UserConfigurableAssetDetails)
            {
                if (item.Number >= slotNum)
                {
                    slotNum = item.Number + 1;
                }
            }

            //Skip odd slots as these seem to be causing issues
            if (slotNum % 2 == 0)
            {
                slotNum++;
            }

            Debug.Console(1, string.Format("Next available fusion asset number is: {0}", slotNum));

            return slotNum;
        }

        #region Overrides of EssentialsBridgeableDevice

        public void GetRoomConfig()
        {
            try
            {
                if (FusionSymbol.IsOnline)
                {
                    string fusionRoomConfigRequest =
                        string.Format(
                            "<RequestRoomConfiguration><RequestID>RoomConfigurationRequest</RequestID><CustomProperties><Property></Property></CustomProperties></RequestRoomConfiguration>");

                    Debug.Console(1, this, "Room Request: {0}", fusionRoomConfigRequest);
                    FusionSymbol.ExtenderFusionRoomDataReservedSigs.RoomConfigQuery.StringValue =
                        fusionRoomConfigRequest;
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "GetRoomConfig Error {0}", e);
            }
        }

        #endregion

        #region ILogStringsWithLevel Members

        public void SendToLog(IKeyed device, Debug.ErrorLogLevel level, string logMessage)
        {
            int fusionLevel;
            switch (level)
            {
                case Debug.ErrorLogLevel.Error:
                {
                    fusionLevel = 3;
                    break;
                }
                case Debug.ErrorLogLevel.Notice:
                {
                    fusionLevel = 1;
                    break;
                }
                case Debug.ErrorLogLevel.Warning:
                {
                    fusionLevel = 2;
                    break;
                }
                case Debug.ErrorLogLevel.None:
                {
                    fusionLevel = 0;
                    break;
                }
                default:
                {
                    fusionLevel = 0;
                    break;
                }
            }

            string tempLogMessage = string.Format("{0}:{1}", fusionLevel, logMessage);
            long errorlogThrottleTime = 60000;
            if (ErrorLogLastMessageSent != tempLogMessage)
            {
                ErrorLogLastMessageSent = tempLogMessage;
                if (ErrorLogTimer == null)
                {
                    ErrorLogTimer = new CTimer(o =>
                    {
                        Debug.Console(2, this, "Sent Message {0}", ErrorLogLastMessageSent);
                        FusionSymbol.ErrorMessage.InputSig.StringValue = ErrorLogLastMessageSent;
                    }, errorlogThrottleTime);
                }
                else
                {
                    ErrorLogTimer.Reset(errorlogThrottleTime);
                }
            }
        }

        #endregion

        #region ILogStrings Members

        public void SendToLog(IKeyed device, string logMessage)
        {
            FusionSymbol.LogText.InputSig.StringValue = logMessage;
        }

        #endregion

        private void RoomConfigParseData(string data)
        {
            data = data.Replace("&", "and");

            try
            {
                XmlDocument roomConfigResponse = new XmlDocument();

                roomConfigResponse.LoadXml(data);

                XmlElement requestRoomConfiguration = roomConfigResponse["RoomConfigurationResponse"];

                if (requestRoomConfiguration != null)
                {
                    foreach (XmlElement e in roomConfigResponse.FirstChild.ChildNodes)
                    {
                        if (e.Name == "RoomInformation")
                        {
                            XmlReader roomInfo = new XmlReader(e.OuterXml);

                            RoomInformation = CrestronXMLSerialization.DeSerializeObject<RoomInformation>(roomInfo);
                            KeyValuePair<uint, DynFusionSerialAttribute> attirbute =
                                SerialAttributesFromFusion.SingleOrDefault(x => x.Value.Name == "Name");

                            Debug.Console(1, "Got fusion room name: {0}", RoomInformation.Name);

                            if (attirbute.Value != null && RoomInformation.Name.Length > 0)
                            {
                                attirbute.Value.StringValue = RoomInformation.Name;
                            }
                        }
                        else if (e.Name == "CustomFields")
                        {
                            foreach (XmlElement el in e)
                            {
                                string id = el.Attributes["ID"].Value;

                                string type = el.SelectSingleNode("CustomFieldType").InnerText;
                                string val = el.SelectSingleNode("CustomFieldValue").InnerText;
                                if (type == "Boolean")
                                {
                                    KeyValuePair<uint, DynFusionDigitalAttribute> attribute =
                                        DigitalAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null)
                                    {
                                        attribute.Value.BoolValue = bool.Parse(val);
                                    }
                                }
                                else if (type == "Integer")
                                {
                                    KeyValuePair<uint, DynFusionAnalogAttribute> attribute =
                                        AnalogAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null)
                                    {
                                        attribute.Value.UShortValue = uint.Parse(val);
                                    }
                                }
                                else if (type == "String" || type == "Text" || type == "URL")
                                {
                                    KeyValuePair<uint, DynFusionSerialAttribute> attribute =
                                        SerialAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null)
                                    {
                                        attribute.Value.StringValue = val;
                                    }
                                }

                                Debug.Console(2, this, "RoomConfigParseData {0} {1} {2}", type, id, val);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "GetRoomConfig Error {0}", e);
            }
            finally
            {
                if (RoomInformationUpdated != null)
                {
                    RoomInformationUpdated(this, new EventArgs());
                }
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            bridge.Eisc.OnlineStatusChange += new OnlineStatusChangeEventHandler(Eisc_OnlineStatusChange);
            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);
            DynFusionJoinMap joinMap = new DynFusionJoinMap(joinStart);

            FusionOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            foreach (KeyValuePair<uint, DynFusionDigitalAttribute> att in DigitalAttributesToFusion)
            {
                DynFusionDigitalAttribute attLocal = att.Value;
                trilist.SetBoolSigAction(attLocal.JoinNumber, (b) => { attLocal.BoolValue = b; });
            }

            foreach (KeyValuePair<uint, DynFusionDigitalAttribute> att in DigitalAttributesFromFusion)
            {
                DynFusionDigitalAttribute attLocal = att.Value;
                attLocal.BoolValueFeedback.LinkInputSig(trilist.BooleanInput[attLocal.JoinNumber]);
            }

            foreach (KeyValuePair<uint, DynFusionAnalogAttribute> att in AnalogAttributesToFusion)
            {
                DynFusionAnalogAttribute attLocal = att.Value;
                trilist.SetUShortSigAction(attLocal.JoinNumber, (a) => { attLocal.UShortValue = a; });
            }

            foreach (KeyValuePair<uint, DynFusionAnalogAttribute> att in AnalogAttributesFromFusion)
            {
                DynFusionAnalogAttribute attLocal = att.Value;
                attLocal.UShortValueFeedback.LinkInputSig(trilist.UShortInput[attLocal.JoinNumber]);
            }

            foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in SerialAttributesToFusion)
            {
                DynFusionSerialAttribute attLocal = att.Value;
                trilist.SetStringSigAction(attLocal.JoinNumber, (a) => { attLocal.StringValue = a; });
            }

            foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in SerialAttributesFromFusion)
            {
                DynFusionSerialAttribute attLocal = att.Value;
                attLocal.StringValueFeedback.LinkInputSig(trilist.StringInput[attLocal.JoinNumber]);
            }

            if (OccSensors != null)
            {
                foreach (DynFusionAssetOccupancySensor occSensor in OccSensors)
                {
                    occSensor.LinkApi(trilist, joinStart);
                }
            }

            //HelpRequest
            HelpRequest.HelpMessageFromFusionEvent += (o, a) =>
            {
                trilist.BooleanInput[joinMap.HelpRequestActiveFb.JoinNumber].BoolValue = a.Active > 0;
                trilist.StringInput[joinMap.HelpMsg.JoinNumber].StringValue = a.StringVal;
            };
            HelpRequest.ClearHelpEvent += (o, a) =>
            {
                trilist.BooleanInput[joinMap.HelpRequestActiveFb.JoinNumber].BoolValue = false;
                trilist.StringInput[joinMap.HelpMsg.JoinNumber].StringValue = "";
            };
            trilist.SetSigTrueAction(joinMap.HelpRequestCancel.JoinNumber, () => HelpRequest.CancelRequest("User"));
            trilist.SetStringSigAction(joinMap.HelpMsg.JoinNumber, (a) => HelpRequest.CreateRequest(a, "User"));
            trilist.SetSigTrueAction(joinMap.HelpRequestUpdate.JoinNumber, () => HelpRequest.GetOpenItems());
            //Help Request End

            trilist.SetSigTrueAction(joinMap.RoomConfig.JoinNumber, () => GetRoomConfig());

            if (DeviceUsage != null)
            {
                foreach (KeyValuePair<string, DynFusionDeviceUsage.UsageInfo> device in DeviceUsage.usageInfoDict)
                {
                    switch (device.Value.usageType)
                    {
                        case DynFusionDeviceUsage.UsageType.Display:
                        {
                            ushort x = device.Value.joinNumber;
                            trilist.SetUShortSigAction(device.Value.joinNumber,
                                (args) => DeviceUsage.changeSource(x, args));
                            break;
                        }
                        case DynFusionDeviceUsage.UsageType.Device:
                        {
                            ushort x = device.Value.joinNumber;
                            trilist.SetBoolSigAction(device.Value.joinNumber,
                                (args) => DeviceUsage.StartStopDevice(x, args));
                            break;
                        }
                    }
                }
            }

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (a.DeviceOnLine)
                {
                    GetRoomConfig();
                    foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in SerialAttributesFromFusion)
                    {
                        DynFusionSerialAttribute attLocal = att.Value;
                        BasicTriList trilistLocal = o as BasicTriList;
                        trilistLocal.StringInput[attLocal.JoinNumber].StringValue = attLocal.StringValue;
                    }
                }
            };

            trilist.SetStringSigAction(joinMap.ErrorMsg.JoinNumber, (o) =>
            {
                FusionSymbol.ErrorMessage.InputSig.StringValue = o;
                ErrorLog.Notice("Fusion Error Message: {0}", o);
            });
        }

        private void Eisc_OnlineStatusChange(object o, OnlineOfflineEventArgs args)
        {
            if (args.DeviceOnLine == true)
            {
                if (EiscOfflineTimer != null)
                {
                    EiscOfflineTimer.Stop();
                }

                FusionOnlineFeedback.FireUpdate();

                HelpRequest.Clear();
                HelpRequest.GetOpenItems();
            }
            else
            {
                if (EiscOfflineTimer == null)
                {
                    EiscOfflineTimer = new CTimer(EiscOfflineTimerExpired, 300000); //5 minute timer
                }
                else
                {
                    EiscOfflineTimer.Reset();
                }
            }
        }

        private void EiscOfflineTimerExpired(object o)
        {
            FusionSymbol.ErrorMessage.InputSig.StringValue = "2: Error! Slot 2 Offline";
            ErrorLog.Notice("Fusion Error Message: 2: Error! Slot 2 Offline");
        }
    }

    public static class FusionUuid
    {
        public static string GenerateUuid(string key)
        {
            try
            {
                //Make version 3 UUID instead of guid, this way it remains the same after reboot
                string mac =
                    CrestronEthernetHelper.GetEthernetParameter(
                        CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_MAC_ADDRESS, 0);
                string hash = MD5.Calculate(Encoding.GetEncoding(28591).GetBytes(mac + key));
                string uuid = Regex.Replace(hash, "(.{8})(.{4})(.{4})(.{4})(.{12})", "$1-$2-$3-$4-$5");
                Debug.Console(0, "Generated fusion uuid for mac+key {0}: {1}", mac + key, uuid);
                return uuid;
            }
            catch
            {
                string guid = Guid.NewGuid().ToString();
                Debug.Console(0, "Uuid generation failed for key {0}, using random guid: {1}", key, guid);
                return guid;
            }
        }
    }

    public class RoomInformation
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string TimeZone { get; set; }
        public string WebcamURL { get; set; }
        public string BacklogMsg { get; set; }
        public string SubErrorMsg { get; set; }
        public string EmailInfo { get; set; }
        public List<FusionCustomProperty> FusionCustomProperties { get; set; }

        public RoomInformation()
        {
            FusionCustomProperties = new List<FusionCustomProperty>();
        }
    }
}