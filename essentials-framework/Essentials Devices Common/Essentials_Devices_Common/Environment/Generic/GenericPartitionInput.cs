using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.Core.PartitionSensor;
using PepperDash.Essentials.Core.Bridges.JoinMaps;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    public class GenericPartitionInput : GenericVersiportDigitalInputDevice
    {
        private readonly bool invertInput;
        private readonly GenericPartitionInputConfig PropertiesConfig;

        public GenericPartitionInput(string key, string name, GenericPartitionInputConfig props)
            : base(key, name, props)
        {
            invertInput = props.InvertInput;
            PropertiesConfig = props;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GlsPartitionSensorJoinMap joinMap = new GlsPartitionSensorJoinMap(joinStart);
            trilist.BooleanInput[joinMap.IsOnline.JoinNumber].BoolValue = true;
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

            if (!invertInput)
            {
                InputStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PartitionSensed.JoinNumber]);
                InputStateFeedback.LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.PartitionNotSensed.JoinNumber]);
            }
            else
            {
                InputStateFeedback.LinkComplementInputSig(
                    trilist.BooleanInput[joinMap.PartitionSensed.JoinNumber]);
                InputStateFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PartitionNotSensed.JoinNumber]);
            }

            //Partition controller joins
            trilist.StringInput[joinMap.State.JoinNumber].StringValue = PropertiesConfig.State;

            if (PropertiesConfig.Divided != null)
            {
                trilist.UShortInput[joinMap.DividedPreset.JoinNumber].UShortValue = PropertiesConfig.Divided.Preset;
                trilist.StringInput[joinMap.DividedRoomKey.JoinNumber].StringValue =
                    PropertiesConfig.Divided.Tp01RoomKey;
                trilist.StringInput[joinMap.DividedRoomKey.JoinNumber + 1].StringValue =
                    PropertiesConfig.Divided.Tp02RoomKey;
                trilist.StringInput[joinMap.DividedRoomKey.JoinNumber + 2].StringValue =
                    PropertiesConfig.Divided.Tp03RoomKey;
                trilist.StringInput[joinMap.DividedRoomKey.JoinNumber + 3].StringValue =
                    PropertiesConfig.Divided.Tp04RoomKey;
            }

            if (PropertiesConfig.Combined != null)
            {
                trilist.UShortInput[joinMap.CombinedPreset.JoinNumber].UShortValue = PropertiesConfig.Combined.Preset;
                trilist.StringInput[joinMap.CombinedRoomKey.JoinNumber].StringValue =
                    PropertiesConfig.Combined.Tp01RoomKey;
                trilist.StringInput[joinMap.CombinedRoomKey.JoinNumber + 1].StringValue =
                    PropertiesConfig.Combined.Tp02RoomKey;
                trilist.StringInput[joinMap.CombinedRoomKey.JoinNumber + 2].StringValue =
                    PropertiesConfig.Combined.Tp03RoomKey;
                trilist.StringInput[joinMap.CombinedRoomKey.JoinNumber + 3].StringValue =
                    PropertiesConfig.Combined.Tp04RoomKey;
            }

            InputStateFeedback.FireUpdate();
        }
    }

    public class GenericPartitionInputConfig : IOPortConfig
    {
        [JsonProperty("invertInput")] public bool InvertInput { get; set; }
        [JsonProperty("state")] public string State { get; set; }

        [JsonProperty("divided")] public RoomState Divided { get; set; }

        [JsonProperty("combined")] public RoomState Combined { get; set; }
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

            GenericPartitionInputConfig props =
                JsonConvert.DeserializeObject<GenericPartitionInputConfig>(dc.Properties.ToString());

            if (props == null) return null;

            GenericPartitionInput portDevice = new GenericPartitionInput(dc.Key, dc.Name, props);

            return portDevice;
        }
    }
}