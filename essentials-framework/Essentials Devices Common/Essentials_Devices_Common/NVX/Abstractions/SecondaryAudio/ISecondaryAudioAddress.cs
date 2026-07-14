using UmdEssentials.Core;

namespace NvxEpi.Abstractions.SecondaryAudio
{
    public interface ISecondaryAudioAddress
    {
        StringFeedback SecondaryAudioAddress { get; }
        StringFeedback TxAudioAddress { get; }
        StringFeedback RxAudioAddress { get; }
    }
}