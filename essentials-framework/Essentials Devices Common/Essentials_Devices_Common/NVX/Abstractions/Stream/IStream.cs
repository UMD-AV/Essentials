using NvxEpi.Abstractions.Device;
using UmdEssentials.Core;

namespace NvxEpi.Abstractions.Stream
{
    public interface IStream : INvxDevice, IMulticastAddress
    {
        BoolFeedback IsStreamingVideo { get; }
        StringFeedback VideoStreamStatus { get; }
        StringFeedback StreamUrl { get; }
    }
}