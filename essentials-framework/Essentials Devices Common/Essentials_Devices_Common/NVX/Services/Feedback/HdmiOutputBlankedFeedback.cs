using Crestron.SimplSharpPro.DM.Streaming;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class HdmiOutputBlankEnabledFeedback
    {
        public const string Key = "HdmiOutBlankEnabled";

        public static BoolFeedback GetFeedback(DmNvxBaseClass device)
        {
            if (device.HdmiOut == null)
                return new BoolFeedback(Key, () => false);

            BoolFeedback feedback = new BoolFeedback(Key, () => device.HdmiOut.BlankEnabledFeedback.BoolValue);
            device.HdmiOut.StreamChange += (stream, args) => feedback.FireUpdate();

            return feedback;
        }
    }
}