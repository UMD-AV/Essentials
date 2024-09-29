using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.Core.Bridges.JoinMaps;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    public class GenericPartitionInput : GenericVersiportDigitalInputDevice
    {
        private bool invertInput;

        public GenericPartitionInput(string key, string name, GenericPartitionInputConfig props)
            : base(key, name, props)
        {
            if (props.InvertInput != null)
            {
                invertInput = props.InvertInput;
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GlsPartitionSensorJoinMap(joinStart);
            trilist.BooleanInput[joinMap.IsOnline.JoinNumber].BoolValue = true;
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;

            if (!invertInput)
            {
                this.InputStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PartitionSensed.JoinNumber]);
                this.InputStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PartitionNotSensed.JoinNumber]);
            }
            else
            {
                this.InputStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PartitionSensed.JoinNumber]);
                this.InputStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PartitionNotSensed.JoinNumber]);
            }

            InputStateFeedback.FireUpdate();
        }
    }

    public class GenericPartitionInputConfig : IOPortConfig
    {
        [JsonProperty("invertInput")]
        public bool InvertInput { get; set; }
    }

    public class GenericPartitionInputFactory : EssentialsDeviceFactory<GenericPartitionInput>
    {
        public GenericPartitionInputFactory()
        {
            TypeNames = new List<string>() { "genericpartition" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Generic Partition Input Device");

            var props = JsonConvert.DeserializeObject<GenericPartitionInputConfig>(dc.Properties.ToString());

            if (props == null) return null;

            var portDevice = new GenericPartitionInput(dc.Key, dc.Name, props);

            return portDevice;
        }
    }
}