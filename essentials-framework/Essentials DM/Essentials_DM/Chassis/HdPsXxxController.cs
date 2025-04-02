using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash_Essentials_DM.Config;
using PepperDash_Essentials_Core.Bridges;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PepperDash_Essentials_DM.Chassis
{
    [Description("Wrapper class for all HdPsXxx switchers")]
    public class HdPsXxxController : CrestronGenericBridgeableBaseDevice, IRoutingNumericWithFeedback
    {
        public readonly HdPsXxx Chassis;

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }
        public Dictionary<uint, string> InputNames { get; set; }
        public Dictionary<uint, string> OutputNames { get; set; }
        public Dictionary<uint, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputRouteNameFeedback { get; private set; }
        public Dictionary<uint, BoolFeedback> OutputEndpointOnlineFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public Dictionary<uint, IntFeedback> VideoOutputRouteFeedbacks { get; private set; }

        public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="chassis">HdPs401 device instance</param>
        /// <param name="props"></param>
        public HdPsXxxController(string key, string name, HdPsXxx chassis, HdPsXxxPropertiesConfig props)
            : base(key, name, chassis)
        {
            Chassis = chassis;
            Name = name;

            if (props == null)
            {
                Debug.Console(1, this, "HdPsXxxController properties are null, failed to build device");
                return;
            }

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            InputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            InputNames = new Dictionary<uint, string>();

            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();
            OutputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputRouteNameFeedback = new Dictionary<uint, StringFeedback>();
            OutputNames = new Dictionary<uint, string>();

            OutputEndpointOnlineFeedbacks = new Dictionary<uint, BoolFeedback>();

            VideoInputSyncFeedbacks = new Dictionary<uint, BoolFeedback>();
            VideoOutputRouteFeedbacks = new Dictionary<uint, IntFeedback>();


            InputNames = props.InputNames;
            SetupInputs(InputNames);

            OutputNames = props.OutputNames;
            SetupOutputs(OutputNames);

            Chassis.DMSystemChange += _chassis_SystemChange;
            Chassis.DMInputChange += _chassis_InputChange;
            Chassis.DMOutputChange += _chassis_OutputChange;

            foreach (KeyValuePair<uint, StringFeedback> f in InputNameFeedbacks)
            {
                Feedbacks.Add(f.Value);
            }

            foreach (KeyValuePair<uint, BoolFeedback> f in VideoInputSyncFeedbacks)
            {
                Feedbacks.Add(f.Value);
            }

            foreach (KeyValuePair<uint, StringFeedback> f in OutputNameFeedbacks)
            {
                Feedbacks.Add(f.Value);
            }

            foreach (KeyValuePair<uint, StringFeedback> f in OutputRouteNameFeedback)
            {
                Feedbacks.Add(f.Value);
            }

            foreach (KeyValuePair<uint, IntFeedback> f in VideoOutputRouteFeedbacks)
            {
                Feedbacks.Add(f.Value);
            }

            foreach (KeyValuePair<uint, BoolFeedback> f in OutputEndpointOnlineFeedbacks)
            {
                Feedbacks.Add(f.Value);
            }
        }

        // input setup
        private void SetupInputs(Dictionary<uint, string> dict)
        {
            if (dict == null)
            {
                Debug.Console(1, this, "Failed to setup inputs, properties are null");
                return;
            }

            // iterate through HDMI inputs
            foreach (HdPsXxxHdmiInput item in Chassis.HdmiInputs)
            {
                HdPsXxxHdmiInput input = item;
                uint index = item.Number;
                string key = string.Format("hdmiIn{0}", index);

                SetInputName(index);
                input.Name.StringValue = InputNames[index];

                InputNameFeedbacks.Add(index, new StringFeedback(() => input.NameFeedback.StringValue));

                RoutingInputPort port = new RoutingInputPort(key, eRoutingSignalType.AudioVideo,
                    eRoutingPortConnectionType.Hdmi, input, this)
                {
                    FeedbackMatchObject = input
                };
                Debug.Console(1, this, "Adding Input port: {0} - {1}", port.Key, InputNames[index]);
                InputPorts.Add(port);

                VideoInputSyncFeedbacks.Add(index,
                    new BoolFeedback(() => input.InputPort.SyncDetectedFeedback.BoolValue));
            }

            // iterate through DM Lite inputs
            foreach (HdPsXxxDmLiteInput item in Chassis.DmLiteInputs)
            {
                HdPsXxxDmLiteInput input = item;
                uint index = item.Number;
                string key = string.Format("dmLiteIn{0}", index);

                SetInputName(index);
                input.Name.StringValue = InputNames[index];

                InputNameFeedbacks.Add(index, new StringFeedback(() => input.NameFeedback.StringValue));

                RoutingInputPort port = new RoutingInputPort(key, eRoutingSignalType.AudioVideo,
                    eRoutingPortConnectionType.Hdmi,
                    input, this)
                {
                    FeedbackMatchObject = input
                };
                Debug.Console(1, this, "Adding Input port: {0} - {1}", port.Key, InputNames[index]);
                InputPorts.Add(port);

                VideoInputSyncFeedbacks.Add(index,
                    new BoolFeedback(() => input.InputPort.SyncDetectedFeedback.BoolValue));
            }
        }

        // output setup
        private void SetupOutputs(Dictionary<uint, string> dict)
        {
            if (dict == null)
            {
                Debug.Console(1, this, "Failed to setup outputs, properties are null");
                return;
            }

            foreach (HdPsXxxOutput item in Chassis.HdmiDmLiteOutputs)
            {
                HdPsXxxOutput output = item;
                uint index = item.Number;

                SetOutputName(index);
                output.Name.StringValue = OutputNames[index];

                string hdmiKey = string.Format("hdmiOut{0}", index);
                RoutingOutputPort hdmiPort = new RoutingOutputPort(hdmiKey, eRoutingSignalType.Video,
                    eRoutingPortConnectionType.Hdmi, output, this)
                {
                    FeedbackMatchObject = output,
                    Port = output.HdmiOutput.HdmiOutputPort
                };
                Debug.Console(1, this, "Adding Output port: {0} - {1}", hdmiPort.Key, OutputNames[index]);
                OutputPorts.Add(hdmiPort);

                string dmLiteKey = string.Format("dmLiteOut{0}", index);
                RoutingOutputPort dmLitePort = new RoutingOutputPort(dmLiteKey, eRoutingSignalType.Video,
                    eRoutingPortConnectionType.DmCat, output, this)
                {
                    FeedbackMatchObject = output,
                    Port = output.DmLiteOutput.DmLiteOutputPort
                };
                Debug.Console(1, this, "Adding Output port: {0} - {1}", dmLitePort.Key, OutputNames[index]);
                OutputPorts.Add(dmLitePort);

                OutputNameFeedbacks.Add(index,
                    new StringFeedback(() => output.NameFeedback == null ? "" : output.NameFeedback.StringValue));

                OutputRouteNameFeedback.Add(index,
                    new StringFeedback(() =>
                        output.VideoOutFeedback == null ? "" : output.VideoOutFeedback.NameFeedback.StringValue));

                VideoOutputRouteFeedbacks.Add(index,
                    new IntFeedback(() => output.VideoOutFeedback == null ? 0 : (int)output.VideoOutFeedback.Number));

                OutputEndpointOnlineFeedbacks.Add(index,
                    new BoolFeedback(() => output.DmLiteOutput != null && output.DmLiteOutput.EndpointOnlineFeedback));
            }
        }

        private void SetInputName(uint index)
        {
            if (InputNames.ContainsKey(index))
            {
                if (string.IsNullOrEmpty(InputNames[index]))
                {
                    InputNames[index] = string.Format("Input{0}", index);
                }
            }
            else
            {
                InputNames.Add(index, string.Format("Input{0}", index));
            }
        }

        private void SetOutputName(uint index)
        {
            if (OutputNames.ContainsKey(index))
            {
                if (string.IsNullOrEmpty(OutputNames[index]))
                {
                    OutputNames[index] = string.Format("Output{0}", index);
                }
            }
            else
            {
                OutputNames.Add(index, string.Format("Output{0}", index));
            }
        }

        public void ListRoutingPorts()
        {
            try
            {
                foreach (RoutingInputPort port in InputPorts)
                {
                    Debug.Console(0, this, @"Input Port Key: {0} Port: {1} Type: {2} ConnectionType: {3} Selector: {4}",
                        port.Key, port.Port, port.Type, port.ConnectionType, port.Selector);
                }

                foreach (RoutingOutputPort port in OutputPorts)
                {
                    Debug.Console(0, this,
                        @"Output Port Key: {0} Port: {1} Type: {2} ConnectionType: {3} Selector: {4}", port.Key,
                        port.Port, port.Type, port.ConnectionType, port.Selector);
                }
            }
            catch (Exception ex)
            {
                Debug.Console(0, this, "ListRoutingPorts Exception Message: {0}", ex.Message);
                Debug.Console(0, this, "ListRoutingPorts Exception StackTrace: {0}", ex.StackTrace);
                if (ex.InnerException != null)
                    Debug.Console(0, this, "ListRoutingPorts InnerException: {0}", ex.InnerException);
            }
        }

        #region BridgeLinking

        /// <summary>
        /// Link device to API
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            HdPsXxxControllerJoinMap joinMap = new HdPsXxxControllerJoinMap(joinStart);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this,
                    "Please update config to use 'eiscApiAdvanced' to get all join map features for this device");
            }

            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

            Chassis.OnlineStatusChange += _chassis_OnlineStatusChange;

            LinkChassisInputsToApi(trilist, joinMap);
            LinkChassisOutputsToApi(trilist, joinMap);
        }


        // links inputs to API
        private void LinkChassisInputsToApi(BasicTriList trilist, HdPsXxxControllerJoinMap joinMap)
        {
            for (uint i = 1; i <= Chassis.NumberOfInputs; i++)
            {
                VideoInputSyncFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.InputSync.JoinNumber + i]);

                InputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + i]);

                InputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputVideoNames.JoinNumber + i]);
            }
        }

        // links outputs to API
        private void LinkChassisOutputsToApi(BasicTriList trilist, HdPsXxxControllerJoinMap joinMap)
        {
            for (uint i = 1; i <= Chassis.HdmiDmLiteOutputs.Count; i++)
            {
                uint output = i;

                trilist.SetUShortSigAction(joinMap.OutputRoute.JoinNumber + i, (a) =>
                    ExecuteNumericSwitch(a, (ushort)output, eRoutingSignalType.Video));

                OutputNameFeedbacks[output]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + output]);
                OutputNameFeedbacks[output]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputVideoNames.JoinNumber + output]);
                OutputRouteNameFeedback[output]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + output]);
                OutputEndpointOnlineFeedbacks[output]
                    .LinkInputSig(trilist.BooleanInput[joinMap.OutputEndpointOnline.JoinNumber + output]);
                VideoOutputRouteFeedbacks[output]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputRoute.JoinNumber + output]);
            }
        }

        #endregion


        /// <summary>
        /// Executes a device switch using objects
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        /// <param name="signalType"></param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            HdPsXxxInput input = inputSelector as HdPsXxxInput;
            HdPsXxxOutput output = outputSelector as HdPsXxxOutput;

            Debug.Console(2, this, "ExecuteSwitch: input={0}, output={1}", input, output);

            if (output == null)
            {
                Debug.Console(0, this, "Unable to make switch, output selector is not HdPsXxxHdmiOutput");
                return;
            }

            DMInput current = output.VideoOut;
            if (current != input)
                output.VideoOut = input;
        }


        /// <summary>
        /// Executes a device switch using numeric values
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        /// <param name="signalType"></param>
        public void ExecuteNumericSwitch(ushort inputSelector, ushort outputSelector, eRoutingSignalType signalType)
        {
            DMInput input = inputSelector == 0 ? null : Chassis.Inputs[inputSelector];
            DMOutput output = Chassis.Outputs[outputSelector];

            Debug.Console(2, this, "ExecuteNumericSwitch: input={0}, output={1}", input, output);

            ExecuteSwitch(input, output, signalType);
        }

        #region Events

        // Chassis online/offline event
        private void _chassis_OnlineStatusChange(GenericBase currentDevice,
            OnlineOfflineEventArgs args)
        {
            IsOnline.FireUpdate();

            if (!args.DeviceOnLine) return;

            foreach (Feedback feedback in Feedbacks)
            {
                feedback.FireUpdate();
            }

            Chassis.EnableFrontPanelLock();
            Chassis.FollowOutputOff();
            Chassis.PriorityRouteOff();
            Chassis.AutoRouteOff();
        }

        // Chassis system change event
        private void _chassis_SystemChange(Switch device, DMSystemEventArgs args)
        {
            switch (args.EventId)
            {
                case DMSystemEventIds.VideoOutFeedbackEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating VideoOutputFeedbacks for output {1}", args.EventId,
                        args.Index);

                    uint output = args.Index;

                    uint input = Chassis.HdmiDmLiteOutputs[output].VideoOutFeedback == null
                        ? 0
                        : Chassis.HdmiDmLiteOutputs[output].VideoOutFeedback.Number;

                    VideoOutputRouteFeedbacks[output].FireUpdate();
                    OutputRouteNameFeedback[output].FireUpdate();

                    RoutingInputPort inputPort = InputPorts.FirstOrDefault(
                        p => p.FeedbackMatchObject == Chassis.HdmiDmLiteOutputs[output].VideoOutFeedback);

                    RoutingOutputPort outputPort = OutputPorts.FirstOrDefault(
                        p => p.FeedbackMatchObject == Chassis.HdmiDmLiteOutputs[output]);

                    OnSwitchChange(new RoutingNumericEventArgs(
                        output, input, outputPort, inputPort, eRoutingSignalType.Video));
                    break;
                }
                case DMSystemEventIds.HdmiInNameFeedbackEventId:
                case DMSystemEventIds.DmLiteInNameFeedbackEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating name feedbacks", args.EventId);

                    InputNameFeedbacks[args.Index].FireUpdate();
                    break;
                }
                case DMSystemEventIds.OutputNameFeedbackEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating name feedbacks", args.EventId);
                    OutputNameFeedbacks[args.Index].FireUpdate();
                    break;
                }
                default:
                {
                    Debug.Console(1, this, "Uhandled DM System Event ID {0}", args.EventId);
                    break;
                }
            }
        }


        // Chassis input change event
        private void _chassis_InputChange(Switch device, DMInputEventArgs args)
        {
            switch (args.EventId)
            {
                case DMInputEventIds.SourceSyncEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating VideoInputSyncFeedbacks", args.EventId);
                    foreach (KeyValuePair<uint, BoolFeedback> item in VideoInputSyncFeedbacks)
                    {
                        item.Value.FireUpdate();
                    }

                    break;
                }
                default:
                {
                    Debug.Console(1, this, "Uhandled DM Input Event ID {0}", args.EventId);
                    break;
                }
            }
        }

        // Chassis output change event
        private void _chassis_OutputChange(Switch device, DMOutputEventArgs args)
        {
            switch (args.EventId)
            {
                case DMOutputEventIds.EndpointOnlineEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating endpoint online feedback", args.EventId);
                    uint output = args.Number;
                    OutputEndpointOnlineFeedbacks[output].FireUpdate();
                    break;
                }
                default:
                {
                    Debug.Console(1, this, "Unhandled DM Output Event ID {0}", args.EventId);
                    break;
                }
            }
        }


        // Raise an event when the status of a switch object changes.
        private void OnSwitchChange(RoutingNumericEventArgs args)
        {
            EventHandler<RoutingNumericEventArgs> newEvent = NumericSwitchChange;
            if (newEvent != null) newEvent(this, args);
        }

        #endregion


        #region Factory

        public class HdSp401ControllerFactory : EssentialsPluginDeviceFactory<HdPsXxxController>
        {
            public HdSp401ControllerFactory()
            {
                TypeNames = new List<string>() { "hdps401", "hdps402", "hdps621", "hdps622" };
            }

            public override EssentialsDevice BuildDevice(DeviceConfig dc)
            {
                string key = dc.Key;
                string name = dc.Name;
                string type = dc.Type.ToLower();

                Debug.Console(1, "Factory Attempting to create new {0} device", type);

                HdPsXxxPropertiesConfig props =
                    JsonConvert.DeserializeObject<HdPsXxxPropertiesConfig>(dc.Properties.ToString());
                if (props == null)
                {
                    Debug.Console(1, "Factory failed to create new HD-PSXxx device, properties config was null");
                    return null;
                }

                uint ipid = props.Control.IpIdInt;

                switch (type)
                {
                    case ("hdps401"):
                    {
                        return new HdPsXxxController(key, name, new HdPs401(ipid, Global.ControlSystem), props);
                    }
                    case ("hdps402"):
                    {
                        return new HdPsXxxController(key, name, new HdPs402(ipid, Global.ControlSystem), props);
                    }
                    case ("hdps621"):
                    {
                        return new HdPsXxxController(key, name, new HdPs621(ipid, Global.ControlSystem), props);
                    }
                    case ("hdps622"):
                    {
                        return new HdPsXxxController(key, name, new HdPs622(ipid, Global.ControlSystem), props);
                    }
                    default:
                    {
                        Debug.Console(1, "Factory failed to create new {0} device", type);
                        return null;
                    }
                }
            }
        }

        #endregion
    }
}