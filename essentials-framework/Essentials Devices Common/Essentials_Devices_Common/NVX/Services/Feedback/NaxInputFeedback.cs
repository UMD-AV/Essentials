using Crestron.SimplSharpPro.DM.Streaming;
using UmdEssentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class NaxInputFeedback
    {
        public const string Key = "NaxInput";

        public static StringFeedback GetFeedback(DmNvxBaseClass device)
        {
            StringFeedback feedback = new StringFeedback(Key,
                () => device.Control.ActiveDmNaxAudioSourceFeedback.ToString());

            device.BaseEvent += (@base, args) => feedback.FireUpdate();

            return feedback;
        }
    }
}