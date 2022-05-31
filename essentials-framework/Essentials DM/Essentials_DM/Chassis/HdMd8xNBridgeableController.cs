using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.DM.Config;
using Crestron.SimplSharpPro.DM.Cards;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;


namespace PepperDash.Essentials.DM.Chassis
{
	[Description("Wrapper class for all HdMd8xN switchers")]
	public class HdMd8xNBridgeableController : CrestronGenericBridgeableBaseDevice, IRoutingNumericWithFeedback, IHasFeedback
	{
		private HdMd8xN _Chassis;

		public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

		public Dictionary<uint, string> InputNames { get; set; }
		public Dictionary<uint, string> OutputNames { get; set; }

		public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
		public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

		public FeedbackCollection<BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
		public FeedbackCollection<IntFeedback> VideoOutputRouteFeedbacks { get; private set; }
		public FeedbackCollection<StringFeedback> InputNameFeedbacks { get; private set; }
		public FeedbackCollection<StringFeedback> OutputNameFeedbacks { get; private set; }
		public FeedbackCollection<StringFeedback> OutputRouteNameFeedbacks { get; private set; }
		public StringFeedback DeviceNameFeedback { get; private set; }

		#region Constructor

		public HdMd8xNBridgeableController(string key, string name, HdMd8xN chassis,
            HdMdNxM4kEBridgeablePropertiesConfig props)
			: base(key, name, chassis)
		{
			_Chassis = chassis;
		    Name = name;
            _Chassis.EnableAudioBreakaway.BoolValue = true;

			if (props == null)
			{
				Debug.Console(1, this, "HdMd8xNBridgeableController properties are null, failed to build the device");
				return;
			}
			if (props.Inputs != null)
			{
				InputNames = props.Inputs;
			}
			if (props.Outputs != null)
			{
				OutputNames = props.Outputs;
			}

            DeviceNameFeedback = new StringFeedback(()=>Name);		    

			VideoInputSyncFeedbacks = new FeedbackCollection<BoolFeedback>();
			VideoOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
			InputNameFeedbacks = new FeedbackCollection<StringFeedback>();
			OutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
			OutputRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();

			InputPorts = new RoutingPortCollection<RoutingInputPort>();
			OutputPorts = new RoutingPortCollection<RoutingOutputPort>();


            //Inputs - should always be 8 audio/video inputs
			for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
			{
                try
                {
                    var index = i;
                    string inputName = InputNames.ContainsKey(index) ? InputNames[index] : string.Format("Input{0}", index);
                    _Chassis.Inputs[index].Name.StringValue = inputName;

                    InputPorts.Add(new RoutingInputPort(inputName, eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Hdmi, _Chassis.Inputs[index], this)
                    {
                        FeedbackMatchObject = _Chassis.Inputs[index]
                    });
                    VideoInputSyncFeedbacks.Add(new BoolFeedback(inputName, () => _Chassis.Inputs[index].VideoDetectedFeedback.BoolValue));
                    InputNameFeedbacks.Add(new StringFeedback(inputName, () => InputNames[index]));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("Exception creating input {0} on HD-MD8xN Chassis: {1}", i, ex);
                }
			}

            //Outputs. Either 2 outputs (1 audio, 1 audio/video) for HD-MD8x1 or 4 outputs (2 audio, 2 audio/video) for HD-MD8x2
            if (_Chassis.SwitchType == Switch.eDmSwitch.HdMd8x1)
            {
                CreateAvOutput(1);
                CreateAudioOutput(2);
            }
            else if (_Chassis.SwitchType == Switch.eDmSwitch.HdMd8x2)
            {
                CreateAvOutput(1);
                CreateAvOutput(2);
                CreateAudioOutput(3);
                CreateAudioOutput(4);
            }
            else
            {
                ErrorLog.Error("Error creating HD-MD8xN Chassis, switch type does not match HdMd8x1 or HdMd8x2");
            }

			_Chassis.DMInputChange += Chassis_DMInputChange;
			_Chassis.DMOutputChange += Chassis_DMOutputChange;

			AddPostActivationAction(AddFeedbackCollections);
		}

        private void CreateAvOutput(ushort index)
        {
            string outputName = OutputNames.ContainsKey(index) ? OutputNames[index] : string.Format("Output{0}", index);
            OutputPorts.Add(new RoutingOutputPort(OutputNames[index], eRoutingSignalType.AudioVideo,
                eRoutingPortConnectionType.Hdmi, _Chassis.Outputs[index], this)
            {
                FeedbackMatchObject = _Chassis.Outputs[index]
            });
            VideoOutputRouteFeedbacks.Add(new IntFeedback(outputName, () => _Chassis.Outputs[index].VideoOutFeedback == null ? 0 : (int)_Chassis.Outputs[index].VideoOutFeedback.Number));
            OutputNameFeedbacks.Add(new StringFeedback(outputName, () => OutputNames[index]));
            OutputRouteNameFeedbacks.Add(new StringFeedback(outputName, () => _Chassis.Outputs[index].VideoOutFeedback.NameFeedback.StringValue));
        }

        private void CreateAudioOutput(ushort index)
        {
            string outputName = OutputNames.ContainsKey(index) ? OutputNames[index] : string.Format("Output{0}", index);
            OutputPorts.Add(new RoutingOutputPort(OutputNames[index], eRoutingSignalType.Audio,
                eRoutingPortConnectionType.LineAudio, _Chassis.Outputs[index], this)
            {
                FeedbackMatchObject = _Chassis.Outputs[index]
            });
            VideoOutputRouteFeedbacks.Add(new IntFeedback(outputName, () => _Chassis.Outputs[index].AudioOutFeedback == null ? 0 : (int)_Chassis.Outputs[index].AudioOutFeedback.Number));
            OutputNameFeedbacks.Add(new StringFeedback(outputName, () => OutputNames[index]));
            OutputRouteNameFeedbacks.Add(new StringFeedback(outputName, () => _Chassis.Outputs[index].AudioOutFeedback.NameFeedback.StringValue));
        }
		#endregion

		#region Methods

		/// <summary>
		/// Raise an event when the status of a switch object changes.
		/// </summary>
		/// <param name="e">Arguments defined as IKeyName sender, output, input, and eRoutingSignalType</param>
		private void OnSwitchChange(RoutingNumericEventArgs e)
		{
			var newEvent = NumericSwitchChange;
			if (newEvent != null) newEvent(this, e);
		}

		#region PostActivate

		public void AddFeedbackCollections()
		{
            AddFeedbackToList(DeviceNameFeedback);
			AddCollectionsToList(VideoInputSyncFeedbacks);
			AddCollectionsToList(VideoOutputRouteFeedbacks);
			AddCollectionsToList(InputNameFeedbacks, OutputNameFeedbacks, OutputRouteNameFeedbacks);
		}

		#endregion

		#region FeedbackCollection Methods

		//Add arrays of collections
		public void AddCollectionsToList(params FeedbackCollection<BoolFeedback>[] newFbs)
		{
			foreach (FeedbackCollection<BoolFeedback> fbCollection in newFbs)
			{
				foreach (var item in newFbs)
				{
					AddCollectionToList(item);
				}
			}
		}
		public void AddCollectionsToList(params FeedbackCollection<IntFeedback>[] newFbs)
		{
			foreach (FeedbackCollection<IntFeedback> fbCollection in newFbs)
			{
				foreach (var item in newFbs)
				{
					AddCollectionToList(item);
				}
			}
		}

		public void AddCollectionsToList(params FeedbackCollection<StringFeedback>[] newFbs)
		{
			foreach (FeedbackCollection<StringFeedback> fbCollection in newFbs)
			{
				foreach (var item in newFbs)
				{
					AddCollectionToList(item);
				}
			}
		}

		//Add Collections
		public void AddCollectionToList(FeedbackCollection<BoolFeedback> newFbs)
		{
			foreach (var f in newFbs)
			{
				if (f == null) continue;

				AddFeedbackToList(f);
			}
		}

		public void AddCollectionToList(FeedbackCollection<IntFeedback> newFbs)
		{
			foreach (var f in newFbs)
			{
				if (f == null) continue;

				AddFeedbackToList(f);
			}
		}

		public void AddCollectionToList(FeedbackCollection<StringFeedback> newFbs)
		{
			foreach (var f in newFbs)
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
            var input = inputSelector as DMInput;
		    var output = outputSelector as DMOutput;
            Debug.Console(2, this, "ExecuteSwitch: input={0} output={1}", input, output);

		    if (output == null)
		    {
		        Debug.Console(0, this, "Unable to make switch. output selector is not DMOutput");
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
            var input = inputSelector == 0 ? null : _Chassis.Inputs[inputSelector];
		    var output = _Chassis.Outputs[outputSelector];

            Debug.Console(2, this, "ExecuteNumericSwitch: input={0} output={1}", input, output);

			ExecuteSwitch(input, output, signalType);
		}

		#endregion

		#endregion

		#region Bridge Linking

		public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
		{
			var joinMap = new HdMdNxM4kEControllerJoinMap(joinStart);

			var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

			if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<HdMdNxM4kEControllerJoinMap>(joinMapSerialized);

			if (bridge != null)
			{
				bridge.AddJoinMap(Key, joinMap);
			}
			else
			{
				Debug.Console(0, this, "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
			}

			IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
			DeviceNameFeedback.LinkInputSig(trilist.StringInput[joinMap.Name.JoinNumber]);

			for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
			{
				var joinIndex = i - 1;
			    var input = i;
				//Digital
				VideoInputSyncFeedbacks[InputNames[input]].LinkInputSig(trilist.BooleanInput[joinMap.InputSync.JoinNumber + joinIndex]);

				//Serial                
				InputNameFeedbacks[InputNames[input]].LinkInputSig(trilist.StringInput[joinMap.InputName.JoinNumber + joinIndex]);                
			}

			for (uint i = 1; i <= _Chassis.NumberOfOutputs; i++)
			{
				var joinIndex = i - 1;
			    var output = i;
				//Analog
				VideoOutputRouteFeedbacks[OutputNames[output]].LinkInputSig(trilist.UShortInput[joinMap.OutputRoute.JoinNumber + joinIndex]);
				trilist.SetUShortSigAction(joinMap.OutputRoute.JoinNumber + joinIndex, (a) => ExecuteNumericSwitch(a, (ushort) output, eRoutingSignalType.AudioVideo));

				//Serial
				OutputNameFeedbacks[OutputNames[output]].LinkInputSig(trilist.StringInput[joinMap.OutputName.JoinNumber + joinIndex]);
				OutputRouteNameFeedbacks[OutputNames[output]].LinkInputSig(trilist.StringInput[joinMap.OutputRoutedName.JoinNumber + joinIndex]);
			}

			_Chassis.OnlineStatusChange += Chassis_OnlineStatusChange;

			trilist.OnlineStatusChange += (d, args) =>
			{
			    if (!args.DeviceOnLine)  return;             
			};
		}


		#endregion

		#region Events

		void Chassis_OnlineStatusChange(Crestron.SimplSharpPro.GenericBase currentDevice, Crestron.SimplSharpPro.OnlineOfflineEventArgs args)
		{
            IsOnline.FireUpdate();

		    if (!args.DeviceOnLine) return;
	        
            foreach (var feedback in Feedbacks)
	        {
	            feedback.FireUpdate();
	        }
		}

		void Chassis_DMOutputChange(Switch device, DMOutputEventArgs args)
		{
			if (args.EventId != DMOutputEventIds.VideoOutEventId) return;

		    var output = args.Number;

		    var inputNumber = _Chassis.Outputs[output].VideoOutFeedback == null
		        ? 0
		        : _Chassis.Outputs[output].VideoOutFeedback.Number;

		    var outputName = OutputNames[output];

		    var feedback = VideoOutputRouteFeedbacks[outputName];

		    if (feedback == null)
		    {
		        return;
		    }
		    var inPort =
		        InputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.Outputs[output].VideoOutFeedback);
		    var outPort = OutputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.Outputs[output]);

		    feedback.FireUpdate();
		    OnSwitchChange(new RoutingNumericEventArgs(output, inputNumber, outPort, inPort, eRoutingSignalType.AudioVideo));
		}

		void Chassis_DMInputChange(Switch device, DMInputEventArgs args)
		{           
		    switch (args.EventId)
		    {
                case DMInputEventIds.VideoDetectedEventId:
		        {
                    Debug.Console(1, this, "Event ID {0}: Updating VideoInputSyncFeedbacks", args.EventId);
                    foreach (var item in VideoInputSyncFeedbacks)
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
                    foreach (var item in InputNameFeedbacks)
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

		public class HdMd8xNControllerFactory : EssentialsDeviceFactory<HdMd8xNBridgeableController>
		{
			public HdMd8xNControllerFactory()
			{
				TypeNames = new List<string>() { "hdmd8x2", "hdmd8x1" };
			}

			public override EssentialsDevice BuildDevice(DeviceConfig dc)
			{
				Debug.Console(1, "Factory Attempting to create new HD-MD-8xN Device");

                var props = JsonConvert.DeserializeObject<HdMdNxM4kEBridgeablePropertiesConfig>(dc.Properties.ToString());

				var type = dc.Type.ToLower();
				var control = props.Control;
				var ipid = control.IpIdInt;

				switch (type)
				{
					case ("hdmd8x2"):
						return new HdMd8xNBridgeableController(dc.Key, dc.Name, new HdMd8x2(ipid, Global.ControlSystem), props);
					case ("hdmd8x1"):
						return new HdMd8xNBridgeableController(dc.Key, dc.Name, new HdMd8x1(ipid, Global.ControlSystem), props);
					default:
						return null;
				}
			}
		}

		#endregion



	}
}