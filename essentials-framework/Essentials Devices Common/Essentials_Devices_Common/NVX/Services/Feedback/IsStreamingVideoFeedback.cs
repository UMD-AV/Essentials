using Crestron.SimplSharpPro.DM.Streaming;
using UmdEssentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class IsStreamingVideoFeedback
    {
        public const string Key = "IsStreamingVideo";

        public static BoolFeedback GetFeedback(DmNvxBaseClass device)
        {
            BoolFeedback feedback = new BoolFeedback(Key,
                () => device.Control.StartFeedback.BoolValue);

            device.BaseEvent += (@base, args) => feedback.FireUpdate();

            return feedback;
        }
    }
}