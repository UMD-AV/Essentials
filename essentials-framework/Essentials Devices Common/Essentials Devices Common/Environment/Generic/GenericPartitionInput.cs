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
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.Core.Bridges.JoinMaps;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    public class GenericPartitionInput : EssentialsBridgeableDevice
    {
        IDigitalInput digitalInput;

        public GenericPartitionInput(string key, string name, IOPortConfig props)
            : base(key, name)
        {
            digitalInput = new GenericVersiportDigitalInputDevice(key + "-port", name + " port", GenericVersiportDigitalInputDevice.GetVersiportDigitalInput, props);
            if (digitalInput == null)
            {
                digitalInput = new GenericDigitalInputDevice(key + "-port", name + " port", GenericDigitalInputDevice.GetDigitalInput, props);
            }
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GlsPartitionSensorJoinMap(joinStart);
            trilist.BooleanInput[joinMap.IsOnline.JoinNumber].BoolValue = true;
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;

            digitalInput.InputStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PartitionSensed.JoinNumber]);
            digitalInput.InputStateFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PartitionNotSensed.JoinNumber]);
        }
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
            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<IOPortConfig>(dc.Properties.ToString());

            return new GenericPartitionInput(dc.Key, dc.Name, props);
        }
    }
}