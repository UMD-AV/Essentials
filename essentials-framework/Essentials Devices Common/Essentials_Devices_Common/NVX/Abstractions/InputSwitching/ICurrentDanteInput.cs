using NvxEpi.Abstractions.Dante;
using UmdEssentials.Core;

namespace NvxEpi.Abstractions.InputSwitching
{
    public interface ICurrentDanteInput : IDanteAudio
    {
        StringFeedback CurrentDanteInput { get; }
        IntFeedback CurrentDanteInputValue { get; }
    }
}