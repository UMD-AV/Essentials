using Crestron.SimplSharpPro.DeviceSupport;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Interfaces;
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
using UmdEssentials.Devices.Common.DynFusion.StaticAssets;
using UmdEssentials.Devices.Common.Microphones;

namespace DynFusion
{
    public class DynFusionDevice : EssentialsBridgeableDevice, ILogStringsWithLevel, ILogStrings, IDisposable
    {
        public const ushort FusionJoinOffset = 49;

        //DynFusion Joins
        public string CustomResourceConfig { get; set; }

        public event EventHandler<EventArgs> RoomInformationUpdated;

        private readonly DynFusionConfigObjectTemplate _config;
        private readonly Dictionary<uint, DynFusionDigitalAttribute> _digitalAttributesToFusion;
        private readonly Dictionary<uint, DynFusionAnalogAttribute> _analogAttributesToFusion;
        private readonly Dictionary<uint, DynFusionSerialAttribute> _serialAttributesToFusion;
        private readonly Dictionary<uint, DynFusionDigitalAttribute> _digitalAttributesFromFusion;
        private readonly Dictionary<uint, DynFusionAnalogAttribute> _analogAttributesFromFusion;
        private readonly Dictionary<uint, DynFusionSerialAttribute> _serialAttributesFromFusion;
        private readonly Dictionary<uint, StaticAsset> _staticAssets;
        private static DynFusionJoinMap _joinMapStatic;
        private readonly List<DynFusionAssetOccupancySensor> _occSensors;

        public BoolFeedback FusionOnlineFeedback;
        public RoomInformation RoomInformation;

        public DynFusionDeviceUsage DeviceUsage;
        public readonly DynFusionHelpRequest HelpRequest;
        public readonly FusionRoom FusionSymbol;
        private CTimer _errorLogTimer;
        private CTimer _eiscOfflineTimer;
        private CTimer _onlineEventTimer;
        private string _errorLogLastMessageSent;
        private bool _isInitialized;

        public DynFusionDevice(string key, string name, DynFusionConfigObjectTemplate config)
            : base(key, name)
        {
            Debug.Console(0, this, "Constructing new DynFusionDevice instance");
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironment_ProgramStatusEventHandler;
            _config = config;
            _digitalAttributesToFusion = new Dictionary<uint, DynFusionDigitalAttribute>();
            _analogAttributesToFusion = new Dictionary<uint, DynFusionAnalogAttribute>();
            _serialAttributesToFusion = new Dictionary<uint, DynFusionSerialAttribute>();
            _digitalAttributesFromFusion = new Dictionary<uint, DynFusionDigitalAttribute>();
            _analogAttributesFromFusion = new Dictionary<uint, DynFusionAnalogAttribute>();
            _serialAttributesFromFusion = new Dictionary<uint, DynFusionSerialAttribute>();
            _staticAssets = new Dictionary<uint, StaticAsset>();
            _occSensors = new List<DynFusionAssetOccupancySensor>();
            _joinMapStatic = new DynFusionJoinMap(1);
            Debug.Console(2, "Creating Fusion Symbol {0} {1}", _config.control.IpId, Key);

            FusionSymbol = new FusionRoom(_config.control.IpIdInt, Global.ControlSystem, Key,
                FusionUuid.GenerateUuid(key));

            HelpRequest = new DynFusionHelpRequest(FusionSymbol.Help);

            if (FusionSymbol.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                Debug.Console(0, this, "Failure to register Fusion Symbol");

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
                FusionOnlineFeedback = new BoolFeedback(() => FusionSymbol.IsOnline);
                FusionSymbol.OnlineStatusChange += FusionSymbol_OnlineStatusChange;
                _onlineEventTimer = new CTimer(OnlineTimerExpired, Timeout.Infinite); //30 second timer

                // Attribute State Changes 
                FusionSymbol.FusionStateChange += FusionSymbol_FusionStateChange;
                FusionSymbol.ExtenderFusionRoomDataReservedSigs.DeviceExtenderSigChange +=
                    FusionSymbol_RoomDataDeviceExtenderSigChange;

                if (CustomResourceConfig != null)
                    try
                    {
                        DynFusionConfigObjectTemplate customAttrConfig = JObject.Parse(CustomResourceConfig)
                            .ToObject<DynFusionConfigObjectTemplate>();
                        Debug.Console(0, "Fusion embdedded config read");

                        CrestronEnvironment.EthernetEventHandler +=
                            CrestronEnvironment_EthernetEventHandler;

                        // Create Custom Attributes 
                        if (customAttrConfig.CustomAttributes.DigitalAttributes != null)
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.DigitalAttributes)
                            {
                                Debug.Console(0, "Fusion embdedded attribute: {0}", att.Name);
                                FusionSymbol.AddSig(eSigType.Bool, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIoMask(att.RwType));

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    _digitalAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.Name, att.JoinNumber, att.LinkDeviceKey,
                                            att.LinkDeviceMethod, att.LinkDeviceFeedback));
                                    _digitalAttributesToFusion[att.JoinNumber].BoolValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedBooleanSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                    _digitalAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.Name, att.JoinNumber));
                            }

                        if (customAttrConfig.CustomAttributes.AnalogAttributes != null)
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.AnalogAttributes)
                            {
                                FusionSymbol.AddSig(eSigType.UShort, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIoMask(att.RwType));

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    _analogAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.Name, att.JoinNumber));
                                    _analogAttributesToFusion[att.JoinNumber].UShortValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedUShortSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                    _analogAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.Name, att.JoinNumber));
                            }

                        if (customAttrConfig.CustomAttributes.SerialAttributes != null)
                            foreach (DynFusionAttributeBase att in customAttrConfig.CustomAttributes.SerialAttributes)
                            {
                                FusionSymbol.AddSig(eSigType.String, att.JoinNumber - FusionJoinOffset, att.Name,
                                    GetIoMask(att.RwType));
                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Read)
                                {
                                    _serialAttributesToFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.Name, att.JoinNumber));
                                    _serialAttributesToFusion[att.JoinNumber].StringValueFeedback
                                        .LinkInputSig(FusionSymbol
                                            .UserDefinedStringSigDetails[att.JoinNumber - FusionJoinOffset].InputSig);
                                }

                                if (att.RwType == eReadWrite.ReadWrite || att.RwType == eReadWrite.Write)
                                    _serialAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.Name, att.JoinNumber));
                            }


                        if (customAttrConfig.CustomProperties != null)
                        {
                            if (customAttrConfig.CustomProperties.DigitalProperties != null)
                                foreach (FusionCustomProperty att in
                                         customAttrConfig.CustomProperties.DigitalProperties)
                                    _digitalAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionDigitalAttribute(att.ID, att.JoinNumber));

                            if (customAttrConfig.CustomProperties.AnalogProperties != null)
                                foreach (FusionCustomProperty att in customAttrConfig.CustomProperties.AnalogProperties)
                                    _analogAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionAnalogAttribute(att.ID, att.JoinNumber));

                            if (customAttrConfig.CustomProperties.SerialProperties != null)
                                foreach (FusionCustomProperty att in customAttrConfig.CustomProperties.SerialProperties)
                                    _serialAttributesFromFusion.Add(att.JoinNumber,
                                        new DynFusionSerialAttribute(att.ID, att.JoinNumber));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.ConsoleWithLog(0, "Fusion embedded config exception: {0}", ex.Message);
                    }

                // Create Links for Standard joins 
                CreateStandardJoin(_joinMapStatic.SystemPowerOn, FusionSymbol.SystemPowerOn);
                CreateStandardJoin(_joinMapStatic.SystemPowerOff, FusionSymbol.SystemPowerOff);
                CreateStandardJoin(_joinMapStatic.DisplayPowerOn, FusionSymbol.DisplayPowerOn);
                CreateStandardJoin(_joinMapStatic.DisplayPowerOff, FusionSymbol.DisplayPowerOff);
                CreateStandardJoin(_joinMapStatic.MsgBroadcastEnabled, FusionSymbol.MessageBroadcastEnabled);
                CreateStandardJoin(_joinMapStatic.AuthenticationSucceeded, FusionSymbol.AuthenticateSucceeded);
                CreateStandardJoin(_joinMapStatic.AuthenticationFailed, FusionSymbol.AuthenticateFailed);

                CreateStandardJoin(_joinMapStatic.DeviceUsage, FusionSymbol.DisplayUsage);
                CreateStandardJoin(_joinMapStatic.BroadcastMsgType, FusionSymbol.BroadcastMessageType);

                CreateStandardJoin(_joinMapStatic.ErrorMsg, FusionSymbol.ErrorMessage);
                CreateStandardJoin(_joinMapStatic.LogText, FusionSymbol.LogText);

                // Room Data Extender 
                CreateStandardJoin(_joinMapStatic.ActionQuery,
                    FusionSymbol.ExtenderFusionRoomDataReservedSigs.ActionQuery);
                CreateStandardJoin(_joinMapStatic.RoomConfig,
                    FusionSymbol.ExtenderFusionRoomDataReservedSigs.RoomConfigQuery);

                HelpRequest.GetOpenItems();
                DeviceUsageFactory();

                //Static Assets
                FusionSymbol.FusionAssetStateChange += FusionSymbol_FusionAssetStateChange;

                try
                {
                    const string occSensorName = "Room Occupancy";
                    uint tempAssetNumber = GetNextAvailableAssetNumber(FusionSymbol);
                    Debug.Console(2, this,
                        string.Format("Creating occSensor: {0}, {1}", tempAssetNumber, occSensorName));
                    FusionSymbol.AddAsset(eAssetType.OccupancySensor, tempAssetNumber, occSensorName,
                        "Occupancy Sensor", FusionUuid.GenerateUuid(occSensorName));
                    _occSensors.Add(new DynFusionAssetOccupancySensor(Key + "-" + occSensorName, 951, FusionSymbol,
                        tempAssetNumber));
                }
                catch (Exception ex)
                {
                    Debug.ConsoleWithLog(0, this, string.Format("Creating occSensor failed: {0}", ex.Message));
                }

                foreach (IKeyed device in DeviceManager.AllDevices)
                    try
                    {
                        DisplayBase displayDevice = device as DisplayBase;
                        if (displayDevice != null)
                        {
                            uint num = GetNextAvailableAssetNumber(FusionSymbol);
                            _staticAssets.Add(num, new DisplayStaticAsset(displayDevice, num, FusionSymbol));
                            continue;
                        }

                        WirelessMic micDevice = device as WirelessMic;
                        if (micDevice != null)
                        {
                            uint num = GetNextAvailableAssetNumber(FusionSymbol);
                            string name = micDevice.Name;
                            _staticAssets.Add(num,
                                new WirelessMicStaticAsset(name, micDevice, num, FusionSymbol));
                            continue;
                        }

                        ShureMxaDevice shureMxaDevice = device as ShureMxaDevice;
                        if (shureMxaDevice != null)
                        {
                            uint num = GetNextAvailableAssetNumber(FusionSymbol);
                            string name = shureMxaDevice.Name;
                            _staticAssets.Add(num,
                                new ShureMxaStaticAsset(name, shureMxaDevice, num, FusionSymbol));
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Exception creating static asset for device key {0}: {1}", device.Key,
                            ex.Message);
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

        private void DeviceUsageFactory()
        {
            if (_config.DeviceUsage != null)
            {
                DeviceUsage = new DynFusionDeviceUsage(string.Format("{0}-DeviceUsage", Key), this);
                if (_config.DeviceUsage.usageMinThreshold > 0)
                    DeviceUsage.UsageMinThreshold = _config.DeviceUsage.usageMinThreshold;

                if (_config.DeviceUsage.Devices != null && _config.DeviceUsage.Devices.Count > 0)
                    foreach (DeviceUsageDevice device in _config.DeviceUsage.Devices)
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

                if (_config.DeviceUsage.Displays != null && _config.DeviceUsage.Displays.Count > 0)
                    foreach (DisplayUsageDevice display in _config.DeviceUsage.Displays)
                        try
                        {
                            Debug.Console(1, this, "Creating Display: {0}, {1}", display.joinNumber, display.name);
                            DeviceUsage.CreateDisplay(display.joinNumber, display.name);
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "{0}", ex);
                        }

                if (_config.DeviceUsage.Sources != null && _config.DeviceUsage.Sources.Count > 0)
                    foreach (DeviceUsageSoruce source in _config.DeviceUsage.Sources)
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

        private void CreateStandardJoin(JoinDataComplete join, BooleanSigDataFixedName sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
                _digitalAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionDigitalAttribute(join.Metadata.Description, join.JoinNumber));

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                _digitalAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionDigitalAttribute(join.Metadata.Description, join.JoinNumber));
                _digitalAttributesToFusion[join.JoinNumber].BoolValueFeedback.LinkInputSig(sig.InputSig);
            }
        }

        private void CreateStandardJoin(JoinDataComplete join, UShortSigDataFixedName sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
                _analogAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionAnalogAttribute(join.Metadata.Description, join.JoinNumber));

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                _analogAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionAnalogAttribute(join.Metadata.Description, join.JoinNumber));
                _analogAttributesToFusion[join.JoinNumber].UShortValueFeedback.LinkInputSig(sig.InputSig);
            }
        }

        private void CreateStandardJoin(JoinDataComplete join, StringSigDataFixedName sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
                _serialAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                _serialAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
                _serialAttributesToFusion[join.JoinNumber].StringValueFeedback.LinkInputSig(sig.InputSig);
            }
        }

        private void CreateStandardJoin(JoinDataComplete join, StringInputSig sig)
        {
            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.ToSIMPL)
                _serialAttributesFromFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));

            if (join.Metadata.JoinCapabilities == eJoinCapabilities.ToFromSIMPL ||
                join.Metadata.JoinCapabilities == eJoinCapabilities.FromSIMPL)
            {
                _serialAttributesToFusion.Add(join.JoinNumber,
                    new DynFusionSerialAttribute(join.Metadata.Description, join.JoinNumber));
                _serialAttributesToFusion[join.JoinNumber].StringValueFeedback.LinkInputSig(sig);
            }
        }

        private void FusionSymbol_RoomDataDeviceExtenderSigChange(DeviceExtender currentDeviceExtender,
            SigEventArgs args)
        {
            Debug.Console(2, this,
                string.Format("DynFusion DeviceExtenderChange {0} {1} {2} {3}", currentDeviceExtender,
                    args.Sig.Number, args.Sig.Type, args.Sig.StringValue));
            ushort joinNumber = (ushort)args.Sig.Number;

            switch (args.Sig.Type)
            {
                case eSigType.String:
                    DynFusionSerialAttribute output;

                    if (_serialAttributesFromFusion.TryGetValue(joinNumber, out output))
                        output.StringValue = args.Sig.StringValue;

                    if (args.Sig == FusionSymbol.ExtenderFusionRoomDataReservedSigs.RoomConfigResponse &&
                        args.Sig.StringValue != null)
                        RoomConfigParseData(args.Sig.StringValue);

                    break;
            }
        }

        private void FusionSymbol_FusionStateChange(FusionBase device, FusionStateEventArgs args)
        {
            Debug.Console(2, this, "DynFusion FusionStateChange {0} {1}", args.EventId,
                args.UserConfiguredSigDetail.ToString());
            switch (args.EventId)
            {
                case FusionEventIds.SystemPowerOnReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (_digitalAttributesFromFusion.TryGetValue(_joinMapStatic.SystemPowerOn.JoinNumber, out output))
                        if (sigDetails != null)
                            output.BoolValue = sigDetails.OutputSig.BoolValue;

                    break;
                }
                case FusionEventIds.SystemPowerOffReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (_digitalAttributesFromFusion.TryGetValue(_joinMapStatic.SystemPowerOff.JoinNumber, out output))
                        if (sigDetails != null)
                            output.BoolValue = sigDetails.OutputSig.BoolValue;

                    break;
                }
                case FusionEventIds.DisplayPowerOnReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (_digitalAttributesFromFusion.TryGetValue(_joinMapStatic.DisplayPowerOn.JoinNumber, out output))
                        if (sigDetails != null)
                            output.BoolValue = sigDetails.OutputSig.BoolValue;

                    break;
                }
                case FusionEventIds.DisplayPowerOffReceivedEventId:
                {
                    BooleanSigDataFixedName sigDetails = args.UserConfiguredSigDetail as BooleanSigDataFixedName;
                    DynFusionDigitalAttribute output;
                    if (_digitalAttributesFromFusion.TryGetValue(_joinMapStatic.DisplayPowerOff.JoinNumber, out output))
                        if (sigDetails != null)
                            output.BoolValue = sigDetails.OutputSig.BoolValue;

                    break;
                }
                case FusionEventIds.BroadcastMessageTypeReceivedEventId:
                {
                    UShortSigDataFixedName sigDetails = args.UserConfiguredSigDetail as UShortSigDataFixedName;
                    DynFusionAnalogAttribute output;
                    if (_analogAttributesFromFusion.TryGetValue(_joinMapStatic.BroadcastMsgType.JoinNumber, out output))
                        if (sigDetails != null)
                            output.UShortValue = sigDetails.OutputSig.UShortValue;

                    break;
                }
                case FusionEventIds.HelpMessageReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    if (sigDetails != null)
                    {
                        Debug.Console(0, "Help Message: {0}", sigDetails.OutputSig.StringValue);
                        HelpRequest.ParseFeedback(sigDetails.OutputSig.StringValue);
                    }

                    break;
                }
                case FusionEventIds.TextMessageFromRoomReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (_serialAttributesFromFusion.TryGetValue(_joinMapStatic.TextMessage.JoinNumber, out output))
                        if (sigDetails != null)
                            output.StringValue = sigDetails.OutputSig.StringValue;

                    break;
                }
                case FusionEventIds.BroadcastMessageReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (_serialAttributesFromFusion.TryGetValue(_joinMapStatic.BroadcastMsg.JoinNumber, out output))
                        if (sigDetails != null)
                            output.StringValue = sigDetails.OutputSig.StringValue;

                    break;
                }
                case FusionEventIds.GroupMembershipRequestReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (_serialAttributesFromFusion.TryGetValue(_joinMapStatic.GroupMembership.JoinNumber, out output))
                        if (sigDetails != null)
                            output.StringValue = sigDetails.OutputSig.StringValue;

                    break;
                }
                case FusionEventIds.AuthenticateFailedReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (_serialAttributesFromFusion.TryGetValue(_joinMapStatic.AuthenticationFailed.JoinNumber,
                            out output))
                        if (sigDetails != null)
                            output.StringValue = sigDetails.OutputSig.StringValue;

                    break;
                }
                case FusionEventIds.AuthenticateSucceededReceivedEventId:
                {
                    StringSigDataFixedName sigDetails = args.UserConfiguredSigDetail as StringSigDataFixedName;
                    DynFusionSerialAttribute output;
                    if (_serialAttributesFromFusion.TryGetValue(_joinMapStatic.AuthenticationSucceeded.JoinNumber,
                            out output))
                        if (sigDetails != null)
                            output.StringValue = sigDetails.OutputSig.StringValue;

                    break;
                }
                case FusionEventIds.UserConfiguredBoolSigChangeEventId:
                {
                    BooleanSigData sigDetails = args.UserConfiguredSigDetail as BooleanSigData;
                    if (sigDetails != null)
                    {
                        uint joinNumber = sigDetails.Number + FusionJoinOffset;
                        DynFusionDigitalAttribute output;
                        Debug.Console(2, this, "DynFusion UserAttribute Digital Join:{0} Name:{1} Value:{2}",
                            joinNumber,
                            sigDetails.Name, sigDetails.OutputSig.BoolValue);

                        if (_digitalAttributesFromFusion.TryGetValue(joinNumber, out output))
                            output.BoolValue = sigDetails.OutputSig.BoolValue;
                    }

                    break;
                }

                case FusionEventIds.UserConfiguredUShortSigChangeEventId:
                {
                    UShortSigData sigDetails = args.UserConfiguredSigDetail as UShortSigData;
                    if (sigDetails != null)
                    {
                        uint joinNumber = sigDetails.Number + FusionJoinOffset;
                        DynFusionAnalogAttribute output;
                        Debug.Console(2, this, "DynFusion UserAttribute Analog Join:{0} Name:{1} Value:{2}", joinNumber,
                            sigDetails.Name, sigDetails.OutputSig.UShortValue);

                        if (_analogAttributesFromFusion.TryGetValue(joinNumber, out output))
                            output.UShortValue = sigDetails.OutputSig.UShortValue;
                    }

                    break;
                }
                case FusionEventIds.UserConfiguredStringSigChangeEventId:
                {
                    StringSigData sigDetails = args.UserConfiguredSigDetail as StringSigData;
                    if (sigDetails != null)
                    {
                        uint joinNumber = sigDetails.Number + FusionJoinOffset;
                        DynFusionSerialAttribute output;
                        Debug.Console(2, this, "DynFusion UserAttribute Analog Join:{0} Name:{1} Value:{2}", joinNumber,
                            sigDetails.Name, sigDetails.OutputSig.StringValue);

                        if (_serialAttributesFromFusion.TryGetValue(joinNumber, out output))
                            output.StringValue = sigDetails.OutputSig.StringValue;
                    }

                    break;
                }
            }
        }

        private void FusionSymbol_FusionAssetStateChange(FusionBase device, FusionAssetStateEventArgs args)
        {
            Debug.Console(1, this, "DynFusion Asset State Change index:{0}", args.UserConfigurableAssetDetailIndex);
            StaticAsset asset;
            if (_staticAssets.TryGetValue(args.UserConfigurableAssetDetailIndex, out asset))
                asset.FusionAssetStateChange(args);
        }

        private void CrestronEnvironment_EthernetEventHandler(EthernetEventArgs args)
        {
            if (_isInitialized && args.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                switch (args.EthernetEventType)
                {
                    case eEthernetEventType.LinkUp:
                        Debug.Console(0, this, "Ethernet Link Up");
                        FusionSymbol.Register();
                        break;
                    case eEthernetEventType.LinkDown:
                        Debug.Console(0, this, "Ethernet Link Down");
                        FusionSymbol.UnRegister();
                        break;
                }
        }

        private void FusionSymbol_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            FusionOnlineFeedback.FireUpdate();
            if (args.DeviceOnLine)
            {
                Debug.ConsoleWithLog(0, this, "DynFusion Symbol Online");
                _onlineEventTimer.Reset(5000);
            }
            else
            {
                Debug.ConsoleWithLog(0, this, "DynFusion Symbol Offline");
                _onlineEventTimer.Stop();
            }
        }

        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
            Dispose();
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

        private static eSigIoMask GetIoMask(eReadWrite mask)
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

            return type;
        }

        public static uint GetNextAvailableAssetNumber(FusionRoom room)
        {
            uint slotNum = 1;
            foreach (CustomFusionAssetData item in room.UserConfigurableAssetDetails)
                if (item.Number >= slotNum)
                    slotNum = item.Number + 1;

            //Skip odd slots as these seem to be causing issues
            if (slotNum % 2 == 0) slotNum++;

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
                    const string fusionRoomConfigRequest =
                        "<RequestRoomConfiguration><RequestID>RoomConfigurationRequest</RequestID><CustomProperties><Property></Property></CustomProperties></RequestRoomConfiguration>";

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
                default:
                {
                    fusionLevel = 0;
                    break;
                }
            }

            string tempLogMessage = string.Format("{0}:{1}", fusionLevel, logMessage);
            long errorlogThrottleTime = 60000;
            if (_errorLogLastMessageSent != tempLogMessage)
            {
                _errorLogLastMessageSent = tempLogMessage;
                if (_errorLogTimer == null)
                    _errorLogTimer = new CTimer(o =>
                    {
                        Debug.Console(2, this, "Sent Message {0}", _errorLogLastMessageSent);
                        FusionSymbol.ErrorMessage.InputSig.StringValue = _errorLogLastMessageSent;
                    }, errorlogThrottleTime);
                else
                    _errorLogTimer.Reset(errorlogThrottleTime);
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
                    foreach (XmlElement e in roomConfigResponse.FirstChild.ChildNodes)
                        if (e.Name == "RoomInformation")
                        {
                            XmlReader roomInfo = new XmlReader(e.OuterXml);

                            RoomInformation = CrestronXMLSerialization.DeSerializeObject<RoomInformation>(roomInfo);
                            KeyValuePair<uint, DynFusionSerialAttribute> attirbute =
                                _serialAttributesFromFusion.SingleOrDefault(x => x.Value.Name == "Name");

                            Debug.Console(1, "Got fusion room name: {0}", RoomInformation.Name);

                            if (attirbute.Value != null && RoomInformation.Name.Length > 0)
                                attirbute.Value.StringValue = RoomInformation.Name;
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
                                        _digitalAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null) attribute.Value.BoolValue = bool.Parse(val);
                                }
                                else if (type == "Integer")
                                {
                                    KeyValuePair<uint, DynFusionAnalogAttribute> attribute =
                                        _analogAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null) attribute.Value.UShortValue = uint.Parse(val);
                                }
                                else if (type == "String" || type == "Text" || type == "URL")
                                {
                                    KeyValuePair<uint, DynFusionSerialAttribute> attribute =
                                        _serialAttributesFromFusion.SingleOrDefault(x => x.Value.Name == id);

                                    if (attribute.Value != null) attribute.Value.StringValue = val;
                                }

                                Debug.Console(2, this, "RoomConfigParseData {0} {1} {2}", type, id, val);
                            }
                        }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "GetRoomConfig Error {0}", e);
            }
            finally
            {
                if (RoomInformationUpdated != null) RoomInformationUpdated(this, EventArgs.Empty);
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            bridge.Eisc.OnlineStatusChange += Eisc_OnlineStatusChange;
            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);
            DynFusionJoinMap joinMap = new DynFusionJoinMap(joinStart);

            FusionOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Online.JoinNumber]);

            foreach (KeyValuePair<uint, DynFusionDigitalAttribute> att in _digitalAttributesToFusion)
            {
                DynFusionDigitalAttribute attLocal = att.Value;
                trilist.SetBoolSigAction(attLocal.JoinNumber, (b) => { attLocal.BoolValue = b; });
            }

            foreach (KeyValuePair<uint, DynFusionDigitalAttribute> att in _digitalAttributesFromFusion)
            {
                DynFusionDigitalAttribute attLocal = att.Value;
                attLocal.BoolValueFeedback.LinkInputSig(trilist.BooleanInput[attLocal.JoinNumber]);
            }

            foreach (KeyValuePair<uint, DynFusionAnalogAttribute> att in _analogAttributesToFusion)
            {
                DynFusionAnalogAttribute attLocal = att.Value;
                trilist.SetUShortSigAction(attLocal.JoinNumber, (a) => { attLocal.UShortValue = a; });
            }

            foreach (KeyValuePair<uint, DynFusionAnalogAttribute> att in _analogAttributesFromFusion)
            {
                DynFusionAnalogAttribute attLocal = att.Value;
                attLocal.UShortValueFeedback.LinkInputSig(trilist.UShortInput[attLocal.JoinNumber]);
            }

            foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in _serialAttributesToFusion)
            {
                DynFusionSerialAttribute attLocal = att.Value;
                trilist.SetStringSigAction(attLocal.JoinNumber, (a) => { attLocal.StringValue = a; });
            }

            foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in _serialAttributesFromFusion)
            {
                DynFusionSerialAttribute attLocal = att.Value;
                attLocal.StringValueFeedback.LinkInputSig(trilist.StringInput[attLocal.JoinNumber]);
            }

            if (_occSensors != null)
                foreach (DynFusionAssetOccupancySensor occSensor in _occSensors)
                    occSensor.LinkApi(trilist, joinStart);

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

            trilist.SetSigTrueAction(joinMap.RoomConfig.JoinNumber, GetRoomConfig);

            if (DeviceUsage != null)
                foreach (KeyValuePair<string, DynFusionDeviceUsage.UsageInfo> device in DeviceUsage.UsageInfoDict)
                    switch (device.Value.UsageType)
                    {
                        case DynFusionDeviceUsage.UsageType.Display:
                        {
                            ushort x = device.Value.JoinNumber;
                            trilist.SetUShortSigAction(device.Value.JoinNumber,
                                (args) => DeviceUsage.ChangeSource(x, args));
                            break;
                        }
                        case DynFusionDeviceUsage.UsageType.Device:
                        {
                            ushort x = device.Value.JoinNumber;
                            trilist.SetBoolSigAction(device.Value.JoinNumber,
                                (args) => DeviceUsage.StartStopDevice(x, args));
                            break;
                        }
                    }

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (a.DeviceOnLine)
                {
                    GetRoomConfig();
                    foreach (KeyValuePair<uint, DynFusionSerialAttribute> att in _serialAttributesFromFusion)
                    {
                        DynFusionSerialAttribute attLocal = att.Value;
                        BasicTriList trilistLocal = o as BasicTriList;
                        if (trilistLocal != null)
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
            if (args.DeviceOnLine)
            {
                if (_eiscOfflineTimer != null) _eiscOfflineTimer.Stop();

                FusionOnlineFeedback.FireUpdate();

                HelpRequest.Clear();
                HelpRequest.GetOpenItems();
            }
            else
            {
                if (_eiscOfflineTimer == null)
                    _eiscOfflineTimer = new CTimer(EiscOfflineTimerExpired, 300000); //5 minute timer
                else
                    _eiscOfflineTimer.Reset();
            }
        }

        private void EiscOfflineTimerExpired(object o)
        {
            FusionSymbol.ErrorMessage.InputSig.StringValue = "2: Error! Slot 2 Offline";
            ErrorLog.Notice("Fusion Error Message: 2: Error! Slot 2 Offline");
        }

        public void Dispose()
        {
            if (FusionSymbol != null)
            {
                FusionSymbol.UnRegister();
                FusionSymbol.Dispose();
            }

            if (_errorLogTimer != null) _errorLogTimer.Dispose();
            if (_eiscOfflineTimer != null) _eiscOfflineTimer.Dispose();
            if (_onlineEventTimer != null) _onlineEventTimer.Dispose();
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
        public string Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string TimeZone { get; set; }
        public string WebcamUrl { get; set; }
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