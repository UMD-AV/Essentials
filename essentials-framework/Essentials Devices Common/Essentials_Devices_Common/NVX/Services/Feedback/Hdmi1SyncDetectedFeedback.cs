using Crestron.SimplSharpPro.DM.Streaming;
using UmdEssentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class Hdmi1SyncDetectedFeedback
    {
        public const string Key = "Hdmi1SyncDetected";

        public static BoolFeedback GetFeedback(DmNvxBaseClass device)
        {
            if (device.HdmiIn == null || device.HdmiIn[1] == null)
                return new BoolFeedback(() => false);

            BoolFeedback feedback = new BoolFeedback(Key,
                () => device.HdmiIn[1].SyncDetectedFeedback.BoolValue && device.IsOnline);

            device.HdmiIn[1].StreamChange += (stream, args) => feedback.FireUpdate();
            device.OnlineStatusChange += (status, args) => feedback.FireUpdate();
            return feedback;
        }
    }
}