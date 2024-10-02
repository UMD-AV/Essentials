using PepperDash.Essentials.Devices.Common.Codec;

namespace PepperDash.Essentials.Devices.Common.VideoCodec
{
    public interface IJoinCalls
    {
        void JoinCall(CodecActiveCallItem activeCall);
        void JoinAllCalls();
    }
}