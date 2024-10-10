using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Abstractions;
using NvxEpi.Services.Feedback;
using PepperDash.Essentials.Core;

namespace NvxEpi.Features.Hdmi.Input
{
    public class DmInput : HdmiInputBase
    {
        public DmInput(INvxDeviceWithHardware device)
            : base(device)
        {
            const uint inputNumber = 1;

            IntFeedback capability = DmHdcpCapabilityValueFeedback.GetFeedback(device.Hardware);
            _capability.Add(inputNumber, capability);

            BoolFeedback sync = DmSyncDetectedFeedback.GetFeedback(device.Hardware);
            _sync.Add(inputNumber, sync);

            StringFeedback inputResolution =
                HdmiCurrentResolutionFeedback.GetFeedback(device.Hardware, inputNumber);

            _currentResolution.Add(inputNumber, inputResolution);

            StringFeedback capabilityString = HdmiHdcpCapabilityFeedback.GetFeedback(device.Hardware, inputNumber);

            _capabilityString.Add(inputNumber, capabilityString);

            IntFeedback audioChannels = HdmiAudioChannelsFeedback.GetFeedback(device.Hardware, inputNumber);

            _audioChannels.Add(inputNumber, audioChannels);

            StringFeedback audioFormat = HdmiAudioFormatFeedback.GetFeedback(device.Hardware, inputNumber);

            _audioFormat.Add(inputNumber, audioFormat);

            StringFeedback colorSpace = HdmiColorSpaceFeedback.GetFeedback(device.Hardware, inputNumber);

            _colorSpace.Add(inputNumber, colorSpace);

            StringFeedback hdrType = HdmiHdrTypeFeedback.GetFeedback(device.Hardware, inputNumber);

            _hdrType.Add(inputNumber, hdrType);

            StringFeedback hdcpSupport = HdmiHdcpSupportFeedback.GetFeedback(device.Hardware, inputNumber);

            _hdcpSupport.Add(inputNumber, hdcpSupport);

            Feedbacks.Add(hdcpSupport);
            Feedbacks.Add(capability);
            Feedbacks.Add(sync);
            Feedbacks.Add(inputResolution);
            Feedbacks.Add(capabilityString);
            Feedbacks.Add(audioChannels);
            Feedbacks.Add(audioFormat);
            Feedbacks.Add(colorSpace);
            Feedbacks.Add(hdrType);
        }
    }
}