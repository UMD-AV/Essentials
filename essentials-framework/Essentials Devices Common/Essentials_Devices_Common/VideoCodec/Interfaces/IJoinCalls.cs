using UmdEssentials.Devices.Common.Codec;

namespace UmdEssentials.Devices.Common.VideoCodec
{
    public interface IJoinCalls
    {
        void JoinCall(CodecActiveCallItem activeCall);
        void JoinAllCalls();
    }
}