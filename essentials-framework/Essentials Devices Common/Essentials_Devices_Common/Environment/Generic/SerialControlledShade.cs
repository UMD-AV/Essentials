using System.Collections.Generic;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using Newtonsoft.Json;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;
using UmdEssentials.Core.Shades;

namespace UmdEssentials.Devices.Common.Environment.Generic
{
    public class SerialControlledShade : EssentialsBridgeableDevice, IShadesOpenCloseStop
    {
        public IBasicCommunication Communication { get; private set; }
        private readonly string openCommand;
        private readonly string stopCommand;
        private readonly string closeCommand;

        public SerialControlledShade(string key, string name, IBasicCommunication comm,
            SerialControlledShadePropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            openCommand = props.OpenCommand;
            stopCommand = props.StopCommand;
            closeCommand = props.CloseCommand;
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericShadesJoinMap joinMap = new GenericShadesJoinMap(joinStart);

            trilist.StringInput[joinMap.ShadesOpenName.JoinNumber].StringValue = Name + " Open";
            trilist.StringInput[joinMap.ShadesCloseName.JoinNumber].StringValue = Name + " Close";
            trilist.StringInput[joinMap.ShadesStopName.JoinNumber].StringValue = Name + " Stop";

            trilist.SetSigTrueAction(joinMap.ShadesOpen.JoinNumber, Open);
            trilist.SetSigTrueAction(joinMap.ShadesClose.JoinNumber, Close);
            trilist.SetSigTrueAction(joinMap.ShadesStop.JoinNumber, Stop);
        }

        public void SendCommand(string cmd)
        {
            Communication.SendText(cmd);
        }

        public void Open()
        {
            if (openCommand != null)
                SendCommand(openCommand);
        }

        public void Close()
        {
            if (closeCommand != null)
                SendCommand(closeCommand);
        }

        public void Stop()
        {
            if (stopCommand != null)
                SendCommand(stopCommand);
        }
    }

    public class SerialControlledShadePropertiesConfig
    {
        public ControlPropertiesConfig Control { get; set; }
        [JsonProperty("openCommand")] public string OpenCommand { get; set; }
        [JsonProperty("stopCommand")] public string StopCommand { get; set; }
        [JsonProperty("closeCommand")] public string CloseCommand { get; set; }
    }

    public class SerialControlledShadeFactory : EssentialsDeviceFactory<SerialControlledShade>
    {
        public SerialControlledShadeFactory()
        {
            TypeNames = new List<string> { "serialshade" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Serial Controlled Shade Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            SerialControlledShadePropertiesConfig props =
                JsonConvert.DeserializeObject<SerialControlledShadePropertiesConfig>(
                    dc.Properties.ToString());

            return new SerialControlledShade(dc.Key, dc.Name, comm, props);
        }
    }
}