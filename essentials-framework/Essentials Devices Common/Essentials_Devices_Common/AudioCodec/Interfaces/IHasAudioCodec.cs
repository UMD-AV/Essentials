using UmdEssentials.Core;

namespace UmdEssentials.Devices.Common.AudioCodec
{
    /// <summary>
    /// For rooms that have audio codec
    /// </summary>
    public interface IHasAudioCodec : IHasInCallFeedback
    {
        AudioCodecBase AudioCodec { get; }

        ///// <summary>
        ///// Make this more specific
        ///// </summary>
        //List<PepperDash.Essentials.Devices.Common.Codec.CodecActiveCallItem> ActiveCalls { get; }
    }
}