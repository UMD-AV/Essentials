using Newtonsoft.Json;

namespace PepperDash.Essentials.Core.CrestronIO
{
    public class IOPortConfig
    {
        [JsonProperty("portDeviceKey")]
        public string PortDeviceKey { get; set; }
        [JsonProperty("portNumber")]
        public uint PortNumber { get; set; }
        [JsonProperty("disablePullUpResistor")]
        public bool DisablePullUpResistor { get; set; }
    }
    public class RelayPortConfig
    {
        [JsonProperty("portDeviceKey")]
        public string PortDeviceKey { get; set; }
        [JsonProperty("portNumber")]
        public uint PortNumber { get; set; }
        [JsonProperty("relayHoldTimeSeconds")]
        public ushort RelayHoldTimeSeconds { get; set; }
    }
}