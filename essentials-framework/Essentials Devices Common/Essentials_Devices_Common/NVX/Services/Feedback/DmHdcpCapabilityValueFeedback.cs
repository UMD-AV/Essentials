using Crestron.SimplSharpPro.DM.Streaming;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class DmHdcpCapabilityValueFeedback
    {
        public static IntFeedback GetFeedback(DmNvxBaseClass device)
        {
            DmNvxE760x dmDevice = device as DmNvxE760x;
            if (dmDevice == null)
                return new IntFeedback(() => 0);

            IntFeedback feedback = new IntFeedback(Hdmi1HdcpCapabilityValueFeedback.Key,
                () => (int)device.DmIn.HdcpCapability);

            device.DmIn.InputStreamChange += (stream, args) => feedback.FireUpdate();
            device.DmIn.VideoAttributes.AttributeChange += (stream, args) => feedback.FireUpdate();
            return feedback;
        }
    }

    public class DmHdcpCapabilityStateFeedback
    {
        public static IntFeedback GetFeedback(DmNvxBaseClass device)
        {
            DmNvxE760x dmDevice = device as DmNvxE760x;
            if (dmDevice == null)
                return new IntFeedback(() => 0);

            IntFeedback feedback = new IntFeedback(Hdmi1HdcpCapabilityValueFeedback.Key,
                () => (int)device.DmIn.VideoAttributes.HdcpStateFeedback);

            device.DmIn.InputStreamChange += (stream, args) => feedback.FireUpdate();
            device.DmIn.VideoAttributes.AttributeChange += (stream, args) => feedback.FireUpdate();
            return feedback;
        }
    }
}