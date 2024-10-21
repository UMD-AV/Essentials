﻿using Crestron.SimplSharpPro.DM.Streaming;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class StreamUrlFeedback
    {
        public const string Key = "StreamUrl";

        public static StringFeedback GetFeedback(DmNvxBaseClass device)
        {
            StringFeedback feedback = new StringFeedback(Key,
                () => device.Control.ServerUrlFeedback.StringValue);

            device.BaseEvent += (@base, args) => feedback.FireUpdate();
            if (device is DmNvxD3x)
            {
                device.SourceReceive.StreamChange += (stream, args) => feedback.FireUpdate();
            }
            else if (device is DmNvxE3x)
            {
                (device as DmNvxE3x).SourceTransmit.StreamChange += (stream, args) => feedback.FireUpdate();
            }
            else if (device is DmNvxE760x)
            {
                (device as DmNvxE760x).SourceTransmit.StreamChange += (stream, args) => feedback.FireUpdate();
            }
            else
            {
                device.SourceReceive.StreamChange += (stream, args) => feedback.FireUpdate();
                device.SourceTransmit.StreamChange += (stream, args) => feedback.FireUpdate();
            }

            return feedback;
        }
    }
}