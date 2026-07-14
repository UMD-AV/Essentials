using UmdEssentials.Core;

namespace NvxEpi.Abstractions.InputSwitching
{
    public interface ICurrentAudioInput : INvxDeviceWithHardware
    {
        StringFeedback CurrentAudioInput { get; }
        IntFeedback CurrentAudioInputValue { get; }
    }
}