using UmdEssentials.Core;

namespace NvxEpi.Abstractions.SecondaryAudio
{
    public interface ICurrentSecondaryAudioStream : ISecondaryAudioStreamWithHardware
    {
        StringFeedback CurrentSecondaryAudioStreamName { get; }
        IntFeedback CurrentSecondaryAudioStreamId { get; }
    }
}