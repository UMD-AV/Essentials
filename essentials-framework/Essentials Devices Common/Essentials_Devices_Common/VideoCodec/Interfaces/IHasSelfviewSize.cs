using UmdEssentials.Devices.Common.VideoCodec;

namespace UmdEssentials.Core.DeviceTypeInterfaces
{
    public interface IHasSelfviewSize
    {
        StringFeedback SelfviewPipSizeFeedback { get; }

        void SelfviewPipSizeSet(CodecCommandWithLabel size);

        void SelfviewPipSizeToggle();
    }
}