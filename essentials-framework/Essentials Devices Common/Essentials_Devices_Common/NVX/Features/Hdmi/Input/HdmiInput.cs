using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Abstractions;
using NvxEpi.Services.Feedback;
using PepperDash.Essentials.Core;

namespace NvxEpi.Features.Hdmi.Input
{
    public class HdmiInput : HdmiInputBase
    {
        public HdmiInput(INvxDeviceWithHardware device)
            : base(device)
        {
            foreach (uint inputNumber in device.Hardware.HdmiIn.Keys)
            {
                IntFeedback capability = HdmiHdcpCapabilityValueFeedback.GetFeedback(device.Hardware, inputNumber);

                _capability.Add(inputNumber, capability);

                BoolFeedback sync = HdmiSyncDetectedFeedback.GetFeedback(device.Hardware, inputNumber);
                _sync.Add(inputNumber, sync);

                StringFeedback inputResolution =
                    HdmiCurrentResolutionFeedback.GetFeedback(device.Hardware, inputNumber);

                _currentResolution.Add(inputNumber, inputResolution);

                StringFeedback capabilityString =
                    HdmiHdcpCapabilityFeedback.GetFeedback(device.Hardware, inputNumber);

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
}