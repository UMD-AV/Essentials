using Crestron.SimplSharpPro.DM.Streaming;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class MulticastAddressFeedback
    {
        public const string Key = "MulticastAddress";

        public static StringFeedback GetFeedback(DmNvxBaseClass device)
        {
            StringFeedback feedback = new StringFeedback(Key,
                () => device.Control.MulticastAddressFeedback.StringValue);

            device.BaseEvent += (@base, args) => feedback.FireUpdate();
            DmNvx35x nvx35X = device as DmNvx35x;
            if (nvx35X == null)
                return feedback;

            device.SourceReceive.StreamChange += (stream, args) => feedback.FireUpdate();
            device.SourceTransmit.StreamChange += (stream, args) => feedback.FireUpdate();

            return feedback;
        }
    }
}