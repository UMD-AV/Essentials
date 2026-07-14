using PepperDash.Core;

namespace UmdEssentials.Devices.Common
{
    public class SetTopBoxPropertiesConfig
    {
        public bool HasPresets { get; set; }
        public bool HasDvr { get; set; }
        public bool HasDpad { get; set; }
        public bool HasNumeric { get; set; }
        public int IrPulseTime { get; set; }

        public ControlPropertiesConfig Control { get; set; }
    }
}