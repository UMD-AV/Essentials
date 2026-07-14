using UmdEssentials.Core;
using UmdEssentials.DM;

namespace NvxEpi.Abstractions.HdmiOutput
{
    public interface IHdmiOutput : INvxDeviceWithHardware, IHdmiBlanking
    {
        BoolFeedback DisabledByHdcp { get; }
        IntFeedback HorizontalResolution { get; }
        StringFeedback EdidManufacturer { get; }
        BoolFeedback OutputSinkConnected { get; }
        StringFeedback OutputResolution { get; }
    }
}