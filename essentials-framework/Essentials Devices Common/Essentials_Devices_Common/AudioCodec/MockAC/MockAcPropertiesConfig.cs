using Newtonsoft.Json;

namespace UmdEssentials.Devices.Common.AudioCodec
{
    public class MockAcPropertiesConfig
    {
        [JsonProperty("phoneNumber")] public string PhoneNumber { get; set; }
    }
}