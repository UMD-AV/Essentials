using UmdEssentials.Devices.Common.VideoCodec;

namespace UmdEssentials.Core.DeviceTypeInterfaces
{
    public interface IHasSelfviewPosition
    {
        StringFeedback SelfviewPipPositionFeedback { get; }

        void SelfviewPipPositionSet(CodecCommandWithLabel position);

        void SelfviewPipPositionToggle();
    }
}