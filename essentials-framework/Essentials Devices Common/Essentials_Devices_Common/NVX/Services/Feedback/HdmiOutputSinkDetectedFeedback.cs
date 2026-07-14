using Crestron.SimplSharpPro.DM.Streaming;
using UmdEssentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class HdmiOutputSinkConnectedFeedback
    {
        public const string Key = "HdmiOutputSinkConnected";

        public static BoolFeedback GetFeedback(DmNvxBaseClass device)
        {
            if (device.HdmiOut == null)
                return new BoolFeedback(Key, () => false);

            BoolFeedback feedback = new BoolFeedback(Key, () => device.HdmiOut.HotplugDetectedFeedback.BoolValue);
            device.HdmiOut.StreamChange += (stream, args) => feedback.FireUpdate();

            return feedback;
        }
    }
}