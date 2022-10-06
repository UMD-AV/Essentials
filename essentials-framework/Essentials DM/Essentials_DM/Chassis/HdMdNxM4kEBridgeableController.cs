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
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;

namespace PepperDash.Essentials.DM.Chassis
{
	[Description("Wrapper class for all HdMdNxM4E switchers")]
	public class HdMdNxM4kEBridgeableController : CrestronGenericBridgeableBaseDevice, IRoutingNumericWithFeedback, IHasFeedback
	{
		private HdMdNxM _Chassis;
		private HdMd4x14kE _Chassis4x1;

		//IroutingNumericEvent
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
		public FeedbackCollection<IntFeedback> InputHdcpFeedbacks { get; private set; }
		public StringFeedback DeviceNameFeedback { get; private set; }
        public BoolFeedback AutoRouteFeedback { get; private set; }

		#region Constructor

		public HdMdNxM4kEBridgeableController(string key, string name, HdMdNxM chassis,
			HdMdNxM4kEBridgeablePropertiesConfig props)
			: base(key, name, chassis)
		{
			_Chassis = chassis;
		    Name = name;

			if (props == null)
			{
				Debug.Console(1, this, "HdMdNx4keBridgeableController properties are null, failed to build the device");
				return;
			}

            InputNames = new Dictionary<uint, string>();
            OutputNames = new Dictionary<uint, string>();
			if (props.InputNames != null)
			{
				foreach (var kvp in props.InputNames)
				{
					Debug.Console(1, this, "props.Inputs: {0}-{1}", kvp.Key, kvp.Value);
				}
				InputNames = props.InputNames;
			}
			if (props.OutputNames != null)
			{
				foreach (var kvp in props.OutputNames)
				{
					Debug.Console(1, this, "props.Outputs: {0}-{1}", kvp.Key, kvp.Value);
				}
				OutputNames = props.OutputNames;
			}

            DeviceNameFeedback = new StringFeedback(()=>Name);		    

			VideoInputSyncFeedbacks = new FeedbackCollection<BoolFeedback>();
			VideoOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
			InputNameFeedbacks = new FeedbackCollection<StringFeedback>();
			OutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
			OutputRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
			InputHdcpFeedbacks = new FeedbackCollection<IntFeedback>();
		                
			InputPorts = new RoutingPortCollection<RoutingInputPort>();
			OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

			if (_Chassis.NumberOfOutputs == 1)
			{
				_Chassis4x1 = _Chassis as HdMd4x14kE;
                AutoRouteFeedback = new BoolFeedback(() => _Chassis4x1.AutoModeOnFeedback.BoolValue);			    
			}

			for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
			{
				var index = i;
                string inputKey = index.ToString();
                string inputName = string.Format("Input{0}", index);
                if (InputNames[i] != null)
                {
                    inputName = InputNames[i];
                    _Chassis.Inputs[index].Name.StringValue = inputName;                    
                }

				InputPorts.Add(new RoutingInputPort(inputName, eRoutingSignalType.AudioVideo,
					eRoutingPortConnectionType.Hdmi, _Chassis.HdmiInputs[index], this)
				{
					FeedbackMatchObject = _Chassis.HdmiInputs[index]
				});
                VideoInputSyncFeedbacks.Add(new BoolFeedback(inputKey, () => _Chassis.HdmiInputs[index].VideoDetectedFeedback == null ? false : _Chassis.HdmiInputs[index].VideoDetectedFeedback.BoolValue));
                InputNameFeedbacks.Add(new StringFeedback(inputKey, () => _Chassis.HdmiInputs[index].NameFeedback == null ? "" : _Chassis.HdmiInputs[index].NameFeedback.StringValue));
                InputHdcpFeedbacks.Add(new IntFeedback(inputKey, () => _Chassis.HdmiInputs[index].HdmiInputPort.HdcpSupportOnFeedback == null ? 0 : _Chassis.HdmiInputs[index].HdmiInputPort.HdcpSupportOnFeedback.BoolValue ? 1 : 0));
			}

			for (uint i = 1; i <= _Chassis.NumberOfOutputs; i++)
			{
				var index = i;
                string outputKey = index.ToString();
                string outputName = string.Format("Output{0}", index);
                if (OutputNames[i] != null)
                {
                    outputName = OutputNames[i];
                    _Chassis.Outputs[index].Name.StringValue = outputName;              
                }

				OutputPorts.Add(new RoutingOutputPort(outputName, eRoutingSignalType.AudioVideo,
					eRoutingPortConnectionType.Hdmi, _Chassis.HdmiOutputs[index], this)
				{
					FeedbackMatchObject = _Chassis.HdmiOutputs[index]
				});
				VideoOutputRouteFeedbacks.Add(new IntFeedback(outputKey, () => _Chassis.HdmiOutputs[index].VideoOutFeedback == null ? 0 : (int)_Chassis.HdmiOutputs[index].VideoOutFeedback.Number));
                OutputNameFeedbacks.Add(new StringFeedback(outputKey, () => _Chassis.HdmiOutputs[index].NameFeedback == null ? "" : _Chassis.HdmiOutputs[index].NameFeedback.StringValue));
                OutputRouteNameFeedbacks.Add(new StringFeedback(outputKey, () => _Chassis.HdmiOutputs[index].VideoOutFeedback == null ? "" : _Chassis.HdmiOutputs[index].VideoOutFeedback.NameFeedback.StringValue));
			}

			_Chassis.DMInputChange += Chassis_DMInputChange;
			_Chassis.DMOutputChange += Chassis_DMOutputChange;
            _Chassis.OnlineStatusChange += Chassis_OnlineStatusChange;

            DeviceNameFeedback.FireUpdate();
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

        public void SetHdcp(uint port, uint hdcpState)
        {
            if (port > _Chassis.NumberOfInputs) return;
            if (port <= 0) return;
            if (hdcpState > 0)
            {
                _Chassis.HdmiInputs[port].HdmiInputPort.HdcpSupportOn();
                InputHdcpFeedbacks[port.ToString()].FireUpdate();
            }
            else
            {
                _Chassis.HdmiInputs[port].HdmiInputPort.HdcpSupportOff();
                InputHdcpFeedbacks[port.ToString()].FireUpdate();
            }
        }

		public void EnableAutoRoute()
		{
			if (_Chassis.NumberOfInputs != 1) return;

			if (_Chassis4x1 == null) return;

			_Chassis4x1.AutoModeOn();
		}

		public void DisableAutoRoute()
		{
			if (_Chassis.NumberOfInputs != 1) return;

			if (_Chassis4x1 == null) return;

			_Chassis4x1.AutoModeOff();
		}

		#region IRouting Members

		public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
		{		    
            var input = inputSelector as HdMdNxMHdmiInput; //changed from HdMdNxM4kzEHdmiInput;
		    var output = outputSelector as HdMdNxMHdmiOutput;
            Debug.Console(2, this, "ExecuteSwitch: input={0} output={1}", input, output);

		    if (output == null)
		    {
		        Debug.Console(0, this, "Unable to make switch. output selector is not HdMdNxMHdmiOutput");
		        return;
		    }

			// Try to make switch only when necessary.  The unit appears to toggle when already selected.
			var current = output.VideoOut;
		    if (current != input)
		        output.VideoOut = input;		        
		}

		#endregion

		#region IRoutingNumeric Members

		public void ExecuteNumericSwitch(ushort inputSelector, ushort outputSelector, eRoutingSignalType signalType)
		{
            var input = inputSelector == 0 ? null : _Chassis.HdmiInputs[inputSelector];
		    var output = _Chassis.HdmiOutputs[outputSelector];

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

			if (_Chassis4x1 != null)
			{
				trilist.SetSigTrueAction(joinMap.EnableAutoRoute.JoinNumber, () => _Chassis4x1.AutoModeOn());
				trilist.SetSigFalseAction(joinMap.EnableAutoRoute.JoinNumber, () => _Chassis4x1.AutoModeOff());
				AutoRouteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.EnableAutoRoute.JoinNumber]);
			}

			for (uint i = 1; i <= _Chassis.NumberOfInputs; i++)
			{
				var joinIndex = i - 1;
			    var input = i;
                string inputKey = input.ToString();

				//Digital
                VideoInputSyncFeedbacks[inputKey].LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + joinIndex]);

                //Analog
                trilist.SetUShortSigAction(joinMap.HdcpSupportCapability.JoinNumber + joinIndex, (hdcpLevel) => SetHdcp(input, hdcpLevel));
                InputHdcpFeedbacks[inputKey].LinkInputSig(trilist.UShortInput[joinMap.HdcpSupportState.JoinNumber + joinIndex]);

				//Serial                
                InputNameFeedbacks[inputKey].LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + joinIndex]);                
			}

			for (uint i = 1; i <= _Chassis.NumberOfOutputs; i++)
			{
				var joinIndex = i - 1;
			    var output = i;
                string outputKey = output.ToString();

				//Analog
                VideoOutputRouteFeedbacks[outputKey].LinkInputSig(trilist.UShortInput[joinMap.OutputRoute.JoinNumber + joinIndex]);
				trilist.SetUShortSigAction(joinMap.OutputRoute.JoinNumber + joinIndex, (a) => ExecuteNumericSwitch(a, (ushort) output, eRoutingSignalType.AudioVideo));

				//Serial
                OutputNameFeedbacks[outputKey].LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + joinIndex]);
                OutputRouteNameFeedbacks[outputKey].LinkInputSig(trilist.StringInput[joinMap.OutputCurrentInputNames.JoinNumber + joinIndex]);
			}
		}


		#endregion

		#region Events

		void Chassis_OnlineStatusChange(Crestron.SimplSharpPro.GenericBase currentDevice, Crestron.SimplSharpPro.OnlineOfflineEventArgs args)
		{
            Debug.Console(1, this, "Online status change: {0}", args.DeviceOnLine);
            IsOnline.FireUpdate();

		    if (!args.DeviceOnLine) return;

            foreach (DMInput input in _Chassis.Inputs)
            {
                if(InputNames[input.Number] != null)
                {
                    Debug.Console(1, this, "Updating input {0} with name {1}", input.Number, InputNames[input.Number]);
                    input.Name.StringValue = InputNames[input.Number];             
                }
            }

            foreach (DMOutput output in _Chassis.Outputs)
            {
                if (OutputNames[output.Number] != null)
                {
                    Debug.Console(1, this, "Updating output {0} with name {1}", output.Number, OutputNames[output.Number]);
                    output.Name.StringValue = OutputNames[output.Number];
                }
            }
	        
            foreach (var feedback in VideoInputSyncFeedbacks)
	        {
	            feedback.FireUpdate();
	        }
            foreach (var feedback in VideoOutputRouteFeedbacks)
	        {
	            feedback.FireUpdate();
	        }
            foreach (var feedback in InputNameFeedbacks)
	        {
	            feedback.FireUpdate();
	        }
            foreach (var feedback in OutputNameFeedbacks)
	        {
	            feedback.FireUpdate();
	        }
            foreach (var feedback in OutputRouteNameFeedbacks)
	        {
	            feedback.FireUpdate();
	        }
            foreach (var feedback in InputHdcpFeedbacks)
	        {
	            feedback.FireUpdate();
	        }

            if (_Chassis4x1 != null)
                AutoRouteFeedback.FireUpdate();
		}

		void Chassis_DMOutputChange(Switch device, DMOutputEventArgs args)
		{
            switch (args.EventId)
            {
                case DMOutputEventIds.VideoOutEventId:
                    {
                        Debug.Console(1, this, "DM Output {0} VideoOutEventId number {1}", args.Number, args.Index);
                        var output = args.Number;
                        var inputNumber = _Chassis.HdmiOutputs[output].VideoOutFeedback == null
                            ? 0
                            : _Chassis.HdmiOutputs[output].VideoOutFeedback.Number;

                        var feedback = VideoOutputRouteFeedbacks[output.ToString()];

                        if (feedback == null)
                        {
                            return;
                        }
                        var inPort =
                            InputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.HdmiOutputs[output].VideoOutFeedback);
                        var outPort = OutputPorts.FirstOrDefault(p => p.FeedbackMatchObject == _Chassis.HdmiOutputs[output]);

                        feedback.FireUpdate();
                        OnSwitchChange(new RoutingNumericEventArgs(output, inputNumber, outPort, inPort, eRoutingSignalType.AudioVideo));
                        break;
                    }
                case DMOutputEventIds.OutputNameEventId:
                case DMInputEventIds.OutputNameFeedbackEventId:
                    {
                        Debug.Console(1, this, "Output{0} Name {1}", args.Number,
                            _Chassis.HdmiOutputs[args.Number].NameFeedback.StringValue);
                        OutputNameFeedbacks[args.Number.ToString()].FireUpdate();
                        break;
                    }
                default:
                    {
                        Debug.Console(2, this, "Unhandled DM Output Event ID {0}", args.EventId);
                        break;
                    }
            }
		}

		void Chassis_DMInputChange(Switch device, DMInputEventArgs args)
		{           
		    switch (args.EventId)
		    {
                case DMInputEventIds.VideoDetectedEventId:
                case DMInputEventIds.SourceSyncEventId:
		        {
                    Debug.Console(1, this, "Event ID {0}: Updating VideoInputSyncFeedbacks", args.EventId);
                    VideoInputSyncFeedbacks[args.Number.ToString()].FireUpdate();
		            break;
		        }
                case DMInputEventIds.InputNameFeedbackEventId:
                case DMInputEventIds.InputNameEventId:
		        {
		            Debug.Console(1, this, "Input {0} Name {1}", args.Number,
		                _Chassis.HdmiInputs[args.Number].NameFeedback.StringValue);
                    InputNameFeedbacks[args.Number.ToString()].FireUpdate();
		            break;
		        }
                case DMInputEventIds.HdcpSupportOnEventId:
                case DMInputEventIds.HdcpSupportOffEventId:
                {
                    Debug.Console(1, this, "Event ID {0}: Updating InputHdcpFeedbacks", args.EventId);
                    InputHdcpFeedbacks[args.Number.ToString()].FireUpdate();
                    break;
                }
                default:
		        {
                    Debug.Console(2, this, "Unhandled DM Input Event ID {0}", args.EventId);
		            break;
		        }
		    }			
		}

		#endregion

		#region Factory

		public class HdMdNxM4kEControllerFactory : EssentialsDeviceFactory<HdMdNxM4kEBridgeableController>
		{
			public HdMdNxM4kEControllerFactory()
			{
				TypeNames = new List<string>() { "hdmd4x14ke", "hdmd4x24ke", "hdmd6x24ke" };
			}

			public override EssentialsDevice BuildDevice(DeviceConfig dc)
			{
				Debug.Console(1, "Factory Attempting to create new HD-MD-NxM-4K-E Device");

				var props = JsonConvert.DeserializeObject<HdMdNxM4kEBridgeablePropertiesConfig>(dc.Properties.ToString());

				var type = dc.Type.ToLower();
				var ipid = props.Control.IpIdInt;
				var address = props.Control.TcpSshProperties.Address;

				switch (type)
				{
					case ("hdmd4x14ke"):
						return new HdMdNxM4kEBridgeableController(dc.Key, dc.Name, new HdMd4x14kE(ipid, address, Global.ControlSystem), props);
					case ("hdmd4x24ke"):
						return new HdMdNxM4kEBridgeableController(dc.Key, dc.Name, new HdMd4x24kE(ipid, address, Global.ControlSystem), props);
					case ("hdmd6x24ke"):
						return new HdMdNxM4kEBridgeableController(dc.Key, dc.Name, new HdMd6x24kE(ipid, address, Global.ControlSystem), props);
					default:
						return null;
				}
			}
		}

		#endregion



	}
}