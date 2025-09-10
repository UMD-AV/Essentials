using PepperDash.Essentials.Core;
using PepperDash.Essentials.DM;

namespace NvxEpi.Abstractions.HdmiOutput
{
    public interface IHdmiOutput : INvxDeviceWithHardware, IHdmiBlanking
    {
        BoolFeedback DisabledByHdcp { get; }
        IntFeedback HorizontalResolution { get; }
        StringFeedback EdidManufacturer { get; }

        StringFeedback OutputResolution { get; }
    }
}