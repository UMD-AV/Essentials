using System;
using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Abstractions;
using NvxEpi.Services.Feedback;
using PepperDash.Core;
using UmdEssentials.Core;

namespace NvxEpi.Features.Hdmi.Input
{
    public class HdmiUsbcInput : HdmiInputBase
    {
        public HdmiUsbcInput(INvxDeviceWithHardware device)
            : base(device)
        {
            DmNvx38x hardware = Hardware as DmNvx38x;
            if (hardware == null)
                throw new Exception("hardware built doesn't match");
            uint hdmiCount = 0;
            foreach (uint inputNumber in hardware.HdmiIn.Keys)
            {
                Debug.Console(0, this, "Adding input number {0}: {1}", inputNumber, hardware.HdmiIn[inputNumber].Name);
                IntFeedback capability = HdmiHdcpCapabilityValueFeedback.GetFeedback(hardware, inputNumber);

                _capability.Add(inputNumber, capability);

                BoolFeedback sync = HdmiSyncDetectedFeedback.GetFeedback(hardware, inputNumber);
                _sync.Add(inputNumber, sync);

                StringFeedback inputResolution =
                    HdmiCurrentResolutionFeedback.GetFeedback(hardware, inputNumber);

                _currentResolution.Add(inputNumber, inputResolution);

                StringFeedback capabilityString =
                    HdmiHdcpCapabilityFeedback.GetFeedback(hardware, inputNumber);

                _capabilityString.Add(inputNumber, capabilityString);

                IntFeedback audioChannels = HdmiAudioChannelsFeedback.GetFeedback(hardware, inputNumber);

                _audioChannels.Add(inputNumber, audioChannels);

                StringFeedback audioFormat = HdmiAudioFormatFeedback.GetFeedback(hardware, inputNumber);

                _audioFormat.Add(inputNumber, audioFormat);

                StringFeedback colorSpace = HdmiColorSpaceFeedback.GetFeedback(hardware, inputNumber);

                _colorSpace.Add(inputNumber, colorSpace);

                StringFeedback hdrType = HdmiHdrTypeFeedback.GetFeedback(hardware, inputNumber);

                _hdrType.Add(inputNumber, hdrType);

                StringFeedback hdcpSupport = HdmiHdcpSupportFeedback.GetFeedback(hardware, inputNumber);

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
                hdmiCount++;
            }

            foreach (uint inputNumber in hardware.UsbcIn.Keys)
            {
                uint inputIndex = inputNumber + hdmiCount;
                Debug.Console(0, this, "Adding input number {0}: {1}", inputNumber, hardware.UsbcIn[inputNumber].Name);
                IntFeedback capability = UsbcHdcpCapabilityValueFeedback.GetFeedback(hardware, inputNumber);

                _capability.Add(inputIndex, capability);

                BoolFeedback sync = UsbcSyncDetectedFeedback.GetFeedback(hardware, inputNumber);
                _sync.Add(inputIndex, sync);

                StringFeedback inputResolution =
                    HdmiCurrentResolutionFeedback.GetFeedback(hardware, inputNumber);

                _currentResolution.Add(inputIndex, inputResolution);

                Feedbacks.Add(capability);
                Feedbacks.Add(sync);
                Feedbacks.Add(inputResolution);
            }
        }
    }
}