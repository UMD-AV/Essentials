﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Cards;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.DM.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Feedback = PepperDash.Essentials.Core.Feedback;


namespace PepperDash.Essentials.DM.Chassis
{
    [Description("Wrapper class for all DmMd6xN switchers")]
    public class DmMd6xNController : CrestronGenericBridgeableBaseDevice, IRoutingNumericWithFeedback, IHasFeedback
    {
        private DmMd6XN _Chassis;

        public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

        public Dictionary<uint, string> InputNames { get; set; }
        public Dictionary<uint, string> OutputNames { get; set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public FeedbackCollection<BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> VideoOutputRouteFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> AudioOutputRouteFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> InputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }
        public StringFeedback DeviceNameFeedback { get; private set; }

        #region Constructor

        public DmMd6xNController(string key, string name, DmMd6XN chassis,
            DMChassisPropertiesConfig props)
            : base(key, name, chassis)
        {
            _Chassis = chassis;
            Name = name;
            _Chassis.EnableAudioBreakaway.BoolValue = true;

            if (props == null)
            {
                Debug.Console(1, this, "DmMd6xNController properties are null, failed to build the device");
                return;
            }

            InputNames = new Dictionary<uint, string>();
            if (props.InputNames != null)
            {
                InputNames = props.InputNames;
            }

            OutputNames = new Dictionary<uint, string>();
            if (props.OutputNames != null)
            {
                OutputNames = props.OutputNames;
            }

            DeviceNameFeedback = new StringFeedback(() => Name);

            VideoInputSyncFeedbacks = new FeedbackCollection<BoolFeedback>();
            VideoOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            AudioOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            InputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputVideoRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputAudioRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            //Inputs - should always be 6 audio/video inputs
            for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
            {
                try
                {
                    uint index = i;
                    if (!InputNames.ContainsKey(index))
                    {
                        InputNames.Add(index, string.Format("Input{0}", index));
                    }

                    string inputName = InputNames[index];
                    _Chassis.Inputs[index].Name.StringValue = inputName;


                    InputPorts.Add(new RoutingInputPort(inputName, eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Hdmi, _Chassis.Inputs[index], this)
                    {
                        FeedbackMatchObject = _Chassis.Inputs[index]
                    });

                    VideoInputSyncFeedbacks.Add(new BoolFeedback(inputName,
                        () => _Chassis.Inputs[index].VideoDetectedFeedback.BoolValue));
                    InputNameFeedbacks.Add(new StringFeedback(inputName,
                        () => _Chassis.Inputs[index].NameFeedback.StringValue));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Exception creating input {0} on DmMd6xN Chassis: {1}", i, ex);
                }
            }

            //Outputs. Either 6, 4, or 1
            for (uint i = 1; i <= _Chassis.NumberOfOutputs; i++)
            {
                try
                {
                    uint index = i;
                    if (!OutputNames.ContainsKey(index))
                    {
                        OutputNames.Add(index, string.Format("Output{0}", index));
                    }

                    string outputName = OutputNames[index];
                    _Chassis.Outputs[index].Name.StringValue = outputName;

                    OutputPorts.Add(new RoutingOutputPort(outputName, eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Hdmi, _Chassis.Outputs[index], this)
                    {
                        FeedbackMatchObject = _Chassis.Outputs[index]
                    });

                    OutputNameFeedbacks.Add(new StringFeedback(outputName,
                        () => _Chassis.Outputs[index].NameFeedback.StringValue));
                    VideoOutputRouteFeedbacks.Add(new IntFeedback(outputName,
                        () => _Chassis.Outputs[index].VideoOutFeedback == null
                            ? 0
                            : (int)_Chassis.Outputs[index].VideoOutFeedback.Number));
                    AudioOutputRouteFeedbacks.Add(new IntFeedback(outputName,
                        () => _Chassis.Outputs[index].AudioOutFeedback == null
                            ? 0
                            : (int)_Chassis.Outputs[index].AudioOutFeedback.Number));
                    OutputVideoRouteNameFeedbacks.Add(new StringFeedback(outputName,
                        () => _Chassis.Outputs[index].VideoOutFeedback == null
                            ? "None"
                            : _Chassis.Outputs[index].VideoOutFeedback.NameFeedback.StringValue));
                    OutputAudioRouteNameFeedbacks.Add(new StringFeedback(outputName,
                        () => _Chassis.Outputs[index].AudioOutFeedback == null
                            ? "None"
                            : _Chassis.Outputs[index].VideoOutFeedback.NameFeedback.StringValue));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Exception creating output {0} on HD-MD8xN Chassis: {1}", i, ex);
                }
            }

            _Chassis.DMInputChange += Chassis_DMInputChange;
            _Chassis.DMOutputChange += Chassis_DMOutputChange;

            AddPostActivationAction(AddFeedbackCollections);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Raise an event when the status of a switch object changes.
        /// </summary>
        /// <param name="e">Arguments defined as IKeyName sender, output, input, and eRoutingSignalType</param>
        private void OnSwitchChange(RoutingNumericEventArgs e)
        {
            EventHandler<RoutingNumericEventArgs> newEvent = NumericSwitchChange;
            if (newEvent != null) newEvent(this, e);
        }

        #region PostActivate

        public void AddFeedbackCollections()
        {
            AddFeedbackToList(DeviceNameFeedback);
            AddCollectionsToList(VideoInputSyncFeedbacks);
            AddCollectionsToList(VideoOutputRouteFeedbacks, AudioOutputRouteFeedbacks);
            AddCollectionsToList(InputNameFeedbacks, OutputNameFeedbacks, OutputVideoRouteNameFeedbacks,
                OutputAudioRouteNameFeedbacks);
        }

        #endregion

        #region FeedbackCollection Methods

        //Add arrays of collections
        public void AddCollectionsToList(params FeedbackCollection<BoolFeedback>[] newFbs)
        {
            foreach (FeedbackCollection<BoolFeedback> fbCollection in newFbs)
            {
                foreach (FeedbackCollection<BoolFeedback> item in newFbs)
                {
                    AddCollectionToList(item);
                }
            }
        }

        public void AddCollectionsToList(params FeedbackCollection<IntFeedback>[] newFbs)
        {
            foreach (FeedbackCollection<IntFeedback> fbCollection in newFbs)
            {
                foreach (FeedbackCollection<IntFeedback> item in newFbs)
                {
                    AddCollectionToList(item);
                }
            }
        }

        public void AddCollectionsToList(params FeedbackCollection<StringFeedback>[] newFbs)
        {
            foreach (FeedbackCollection<StringFeedback> fbCollection in newFbs)
            {
                foreach (FeedbackCollection<StringFeedback> item in newFbs)
                {
                    AddCollectionToList(item);
                }
            }
        }

        //Add Collections
        public void AddCollectionToList(FeedbackCollection<BoolFeedback> newFbs)
        {
            foreach (BoolFeedback f in newFbs)
            {
                if (f == null) continue;

                AddFeedbackToList(f);
            }
        }

        public void AddCollectionToList(FeedbackCollection<IntFeedback> newFbs)
        {
            foreach (IntFeedback f in newFbs)
            {
                if (f == null) continue;

                AddFeedbackToList(f);
            }
        }

        public void AddCollectionToList(FeedbackCollection<StringFeedback> newFbs)
        {
            foreach (StringFeedback f in newFbs)
            {
                if (f == null) continue;

                AddFeedbackToList(f);
            }
        }

        //Add Individual Feedbacks
        public void AddFeedbackToList(PepperDash.Essentials.Core.Feedback newFb)
        {
            if (newFb == null) return;

            if (!Feedbacks.Contains(newFb))
            {
                Feedbacks.Add(newFb);
            }
        }

        #endregion

        #region IRouting Members

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType sigType)
        {
            DMInput input = inputSelector as DMInput;
            DMOutput output = outputSelector as DMOutput;
            Debug.Console(2, this, "ExecuteSwitch: input={0} output={1} sigType={2}", input, output,
                sigType.ToString());

            if (output == null)
            {
                Debug.Console(0, this, "Unable to make switch. Output selector is not DMOutput");
                return;
            }

            if ((sigType & eRoutingSignalType.Video) == eRoutingSignalType.Video)
            {
                _Chassis.VideoEnter.BoolValue = true;
                if (output != null)
                {
                    output.VideoOut = input;
                }
            }

            if ((sigType & eRoutingSignalType.Audio) == eRoutingSignalType.Audio)
            {
                _Chassis.AudioEnter.BoolValue = true;
                if (output != null)
                {
                    output.AudioOut = input;
                }
            }
        }

        #endregion

        #region IRoutingNumeric Members

        public void ExecuteNumericSwitch(ushort inputSelector, ushort outputSelector, eRoutingSignalType signalType)
        {
            DMInput input = inputSelector == 0 ? null : _Chassis.Inputs[inputSelector];
            Card.DMOCard output = _Chassis.Outputs[outputSelector];

            Debug.Console(2, this, "ExecuteNumericSwitch: input={0} output={1}", input, output);

            ExecuteSwitch(input, output, signalType);
        }

        #endregion

        #endregion

        #region Bridge Linking

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            DmChassisControllerJoinMap joinMap = new DmChassisControllerJoinMap(joinStart);

            string joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<DmChassisControllerJoinMap>(joinMapSerialized);

            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this,
                    "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;

            for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
            {
                uint joinIndex = i - 1;
                uint input = i;
                //Digital
                VideoInputSyncFeedbacks[InputNames[input]]
                    .LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + joinIndex]);

                //Serial                
                InputNameFeedbacks[InputNames[input]]
                    .LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + joinIndex]);
            }

            for (uint i = 1; i <= _Chassis.NumberOfOutputs; i++)
            {
                uint joinIndex = i - 1;
                uint output = i;
                //Analog
                VideoOutputRouteFeedbacks[OutputNames[output]]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputVideo.JoinNumber + joinIndex]);
                trilist.SetUShortSigAction(joinMap.OutputVideo.JoinNumber + joinIndex,
                    (a) => ExecuteNumericSwitch(a, (ushort)output, eRoutingSignalType.Video));
                AudioOutputRouteFeedbacks[OutputNames[output]]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputAudio.JoinNumber + joinIndex]);
                trilist.SetUShortSigAction(joinMap.OutputAudio.JoinNumber + joinIndex,
                    (a) => ExecuteNumericSwitch(a, (ushort)output, eRoutingSignalType.Audio));

                //Serial
                OutputNameFeedbacks[OutputNames[output]]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + joinIndex]);
                OutputVideoRouteNameFeedbacks[OutputNames[output]]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + joinIndex]);
                OutputAudioRouteNameFeedbacks[OutputNames[output]]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentAudioInputNames.JoinNumber + joinIndex]);
            }

            _Chassis.OnlineStatusChange += Chassis_OnlineStatusChange;

            trilist.OnlineStatusChange += (d, args) =>
            {
                if (!args.DeviceOnLine) return;
            };
        }

        #endregion

        #region Events

        void Chassis_OnlineStatusChange(Crestron.SimplSharpPro.GenericBase currentDevice,
            Crestron.SimplSharpPro.OnlineOfflineEventArgs args)
        {
            IsOnline.FireUpdate();

            if (!args.DeviceOnLine) return;

            foreach (Feedback feedback in Feedbacks)
            {
                feedback.FireUpdate();
            }
        }

        void Chassis_DMOutputChange(Switch device, DMOutputEventArgs args)
        {
            switch (args.EventId)
            {
                case DMOutputEventIds.VideoOutEventId:
                {
                    uint output = args.Number;
                    uint inputNumber = _Chassis.Outputs[output].VideoOutFeedback == null
                        ? 0
                        : _Chassis.Outputs[output].VideoOutFeedback.Number;

                    string outputName = OutputNames[output];

                    IntFeedback feedback = VideoOutputRouteFeedbacks[outputName];

                    if (feedback == null)
                    {
                        return;
                    }

                    RoutingInputPort inPort = InputPorts.FirstOrDefault(p =>
                        p.FeedbackMatchObject == _Chassis.Outputs[output].VideoOutFeedback);
                    RoutingOutputPort outPort = OutputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.Outputs[output]);

                    feedback.FireUpdate();
                    OnSwitchChange(new RoutingNumericEventArgs(output, inputNumber, outPort, inPort,
                        eRoutingSignalType.Video));
                    break;
                }
                case DMOutputEventIds.AudioOutEventId:
                {
                    uint output = args.Number;
                    uint inputNumber = _Chassis.Outputs[output].AudioOutFeedback == null
                        ? 0
                        : _Chassis.Outputs[output].AudioOutFeedback.Number;

                    string outputName = OutputNames[output];

                    IntFeedback feedback = AudioOutputRouteFeedbacks[outputName];

                    if (feedback == null)
                    {
                        return;
                    }

                    RoutingInputPort inPort = InputPorts.FirstOrDefault(p =>
                        p.FeedbackMatchObject == _Chassis.Outputs[output].AudioOutFeedback);
                    RoutingOutputPort outPort = OutputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.Outputs[output]);

                    feedback.FireUpdate();
                    OnSwitchChange(new RoutingNumericEventArgs(output, inputNumber, outPort, inPort,
                        eRoutingSignalType.Audio));
                    break;
                }
                case DMOutputEventIds.OutputNameEventId:
                case DMOutputEventIds.NameFeedbackEventId:
                {
                    Debug.Console(1, this, "Event ID {0}:  Updating name feedbacks.", args.EventId);
                    Debug.Console(1, this, "Output {0} Name {1}", args.Number,
                        _Chassis.Outputs[args.Number].NameFeedback.StringValue);
                    foreach (StringFeedback item in OutputNameFeedbacks)
                    {
                        item.FireUpdate();
                    }

                    break;
                }
                default:
                {
                    Debug.Console(1, this, "Unhandled DM Output Event ID {0}", args.EventId);
                    break;
                }
            }
        }

        void Chassis_DMInputChange(Switch device, DMInputEventArgs args)
        {
            switch (args.EventId)
            {
                case DMInputEventIds.VideoDetectedEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating VideoInputSyncFeedbacks", args.EventId);
                    foreach (BoolFeedback item in VideoInputSyncFeedbacks)
                    {
                        item.FireUpdate();
                    }

                    break;
                }
                case DMInputEventIds.InputNameFeedbackEventId:
                case DMInputEventIds.InputNameEventId:
                case DMInputEventIds.NameFeedbackEventId:
                {
                    Debug.Console(1, this, "Event ID {0}:  Updating name feedbacks.", args.EventId);
                    Debug.Console(1, this, "Input {0} Name {1}", args.Number,
                        _Chassis.Inputs[args.Number].NameFeedback.StringValue);
                    foreach (StringFeedback item in InputNameFeedbacks)
                    {
                        item.FireUpdate();
                    }

                    break;
                }
                default:
                {
                    Debug.Console(1, this, "Unhandled DM Input Event ID {0}", args.EventId);
                    break;
                }
            }
        }

        #endregion

        #region Factory

        public class DmMd6xNControllerFactory : EssentialsDeviceFactory<DmMd6xNController>
        {
            public DmMd6xNControllerFactory()
            {
                TypeNames = new List<string>() { "dmmd6x6", "dmmd6x4", "dmmd6x1" };
            }

            public override EssentialsDevice BuildDevice(DeviceConfig dc)
            {
                Debug.Console(1, "Factory Attempting to create new DM-MD6xN Device");

                DMChassisPropertiesConfig props = JsonConvert.DeserializeObject<DMChassisPropertiesConfig>(dc.Properties.ToString());

                string type = dc.Type.ToLower();
                ControlPropertiesConfig control = props.Control;
                uint ipid = control.IpIdInt;

                switch (type)
                {
                    case ("dmmd6x6"):
                        return new DmMd6xNController(dc.Key, dc.Name, new DmMd6x6(ipid, Global.ControlSystem), props);
                    case ("dmmd6x4"):
                        return new DmMd6xNController(dc.Key, dc.Name, new DmMd6x4(ipid, Global.ControlSystem), props);
                    case ("dmmd6x1"):
                        return new DmMd6xNController(dc.Key, dc.Name, new DmMd6x1(ipid, Global.ControlSystem), props);
                    default:
                        return null;
                }
            }
        }

        #endregion
    }
}