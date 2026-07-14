using UmdEssentials.Core;

namespace UmdEssentials.Devices.Common.Codec
{
    /// <summary>
    /// Defines minimum volume controls for a codec device with dialing capabilities
    /// </summary>
    public interface ICodecAudio : IBasicVolumeWithFeedback, IPrivacy
    {
    }
}