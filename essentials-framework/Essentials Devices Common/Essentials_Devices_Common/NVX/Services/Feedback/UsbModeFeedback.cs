﻿using Crestron.SimplSharpPro.DM.Streaming;
using PepperDash.Essentials.Core;

namespace NvxEpi.Services.Feedback
{
    public class UsbModeFeedback
    {
        public const string Key = "UsbMode";

        public static StringFeedback GetFeedback(DmNvxBaseClass device)
        {
            if (device.UsbInput == null)
                return new StringFeedback(() => string.Empty);

            StringFeedback feedback = new StringFeedback(Key, () => device.UsbInput.ModeFeedback.ToString());
            device.UsbInput.UsbInputChange += (sender, args) => feedback.FireUpdate();
            return feedback;
        }
    }
}