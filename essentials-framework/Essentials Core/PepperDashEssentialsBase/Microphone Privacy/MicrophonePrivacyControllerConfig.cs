using System.Collections.Generic;

namespace UmdEssentials.Core.Privacy
{
    public class MicrophonePrivacyControllerConfig
    {
        public List<KeyedDevice> Inputs { get; set; }
        public KeyedDevice GreenLedRelay { get; set; }
        public KeyedDevice RedLedRelay { get; set; }
    }

    public class KeyedDevice
    {
        public string DeviceKey { get; set; }
    }
}