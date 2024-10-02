using NvxEpi.Abstractions;
using NvxEpi.Abstractions.HdmiOutput;
using NvxEpi.Services.Feedback;
using PepperDash.Essentials.Core;

namespace NvxEpi.Features.Hdmi.Output
{
    public class VideowallModeOutput : HdmiOutput, IVideowallMode
    {
        public VideowallModeOutput(INvxDeviceWithHardware device) : base(device)
        {
            VideowallMode = VideowallModeFeedback.GetFeedback(device.Hardware);
            VideoAspectRatioMode = VideoAspectRatioModeFeedback.GetFeedback(device.Hardware);
            device.Feedbacks.Add(VideowallMode);
            device.Feedbacks.Add(VideoAspectRatioMode);
        }

        public IntFeedback VideowallMode { get; private set; }

        public IntFeedback VideoAspectRatioMode { get; private set; }
    }
}