namespace PepperDash.Essentials.Core
{
    public interface IBasicVideoMute
    {
        void VideoMuteToggle();
    }

    public interface IBasicVideoMuteWithFeedback : IBasicVideoMute
    {
        BoolFeedback VideoMuteIsOn { get; }

        void VideoMuteOn();
        void VideoMuteOff();
    }
}