using UmdEssentials.Core;

namespace NvxEpi.Abstractions.InputSwitching
{
    public interface ICurrentVideoInput : INvxDeviceWithHardware
    {
        StringFeedback CurrentVideoInput { get; }
        IntFeedback CurrentVideoInputValue { get; }
    }
}