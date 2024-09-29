﻿using System;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM.Endpoints.Transmitters;
using Newtonsoft.Json;

using PepperDash.Core;

using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace PepperDash.Essentials.DM
{
    /// <summary>
    /// Controller class for suitable for HDBaseT transmitters
    /// </summary>
    [Description("Wrapper Class for HDBaseT devices based on HDTx3CB class")]
    public class HDBaseTTxController : BasicDmTxControllerBase, IRoutingInputsOutputs, IComPorts
    {
        public RoutingInputPort HdmiIn { get; private set; }
        public RoutingOutputPort DmOut { get; private set; }

        public HDBaseTTxController(string key, string name, HDTx3CB tx)
            : base(key, name, tx)
        {
            PreventRegistration = true;

            HdmiIn = new RoutingInputPort(DmPortName.HdmiIn1, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, null, this) { Port = tx };

            DmOut = new RoutingOutputPort(DmPortName.DmOut, eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.DmCat, null, this);

            InputPorts = new RoutingPortCollection<RoutingInputPort> { HdmiIn };
            OutputPorts = new RoutingPortCollection<RoutingOutputPort> { DmOut };

            tx.Register();
        }

        #region IRoutingInputs Members

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        #endregion

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        #endregion
        
        #region IComPorts Members

        public CrestronCollection<ComPort> ComPorts { get { return (Hardware as HDTx3CB).ComPorts; } }
        public int NumberOfComPorts { get { return (Hardware as HDTx3CB).NumberOfComPorts; } }

        #endregion

        #region CrestronBridgeableBaseDevice abstract overrides

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new HDBaseTTxControllerJoinMap(joinStart);

            var joinMapSerialized = JoinMapHelper.GetSerializedJoinMapForDevice(joinMapKey);

            if (!string.IsNullOrEmpty(joinMapSerialized))
                joinMap = JsonConvert.DeserializeObject<HDBaseTTxControllerJoinMap>(joinMapSerialized);


            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
            else
            {
                Debug.Console(0, this, "Please update config to use 'eiscapiadvanced' to get all join map features for this device.");
            }

            Debug.Console(1, this, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));

            this.IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
        }

        #endregion
    }

    public class HDBaseTTxControllerJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "HDBaseT device online feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Name")]
        public JoinDataComplete Name = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata { Description = "DM Tx Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
	
        /// <summary>
		/// Plugin device BridgeJoinMap constructor
		/// </summary>
		/// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public HDBaseTTxControllerJoinMap(uint joinStart)
            : this(joinStart, typeof(HDBaseTTxControllerJoinMap))
		{
		}

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected HDBaseTTxControllerJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}