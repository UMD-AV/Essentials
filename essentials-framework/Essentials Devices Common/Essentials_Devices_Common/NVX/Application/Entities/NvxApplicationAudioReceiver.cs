using System;
using System.Collections.Generic;
using System.Linq;
using NvxEpi.Abstractions;
using NvxEpi.Application.Config;
using NvxEpi.Features.Routing;
using NvxEpi.Features.Streams.Audio;
using NvxEpi.Services.InputSwitching;
using NvxEpi.Services.Feedback;
using NvxEpi.Extensions;
using PepperDash.Essentials.Devices.Common;
using PepperDash.Essentials.Core;

namespace NvxEpi.Application.Entities
{
    public class NvxApplicationAudioReceiver : EssentialsDevice
    {
        private readonly IEnumerable<NvxApplicationAudioTransmitter> _transmitters;
        private readonly int _deviceId;
        private INvxDeviceWithHardware _device;
        private readonly IRoutingSink _amp;
        private StringFeedback _audioName;
        private StringFeedback _currentAudioRouteName;
        private IntFeedback _currentAudioRouteId;

        public int DeviceId
        {
            get { return _deviceId; }
        }

        public INvxDeviceWithHardware Device
        {
            get { return _device; }
        }

        public IRoutingSink Amp
        {
            get { return _amp; }
        }

        public StringFeedback AudioName
        {
            get { return _audioName; }
        }

        public StringFeedback CurrentAudioRouteName
        {
            get { return _currentAudioRouteName; }
        }

        public IntFeedback CurrentAudioRouteId
        {
            get { return _currentAudioRouteId; }
        }

        public NvxApplicationAudioReceiver(string key, NvxApplicationDeviceAudioConfig config, int deviceId,
            IEnumerable<NvxApplicationAudioTransmitter> transmitters)
            : base(key)
        {
            _transmitters = transmitters;
            _deviceId = deviceId;
            Amplifier sink = new Amplifier(key + "--amp", key + "--amp");
            _amp = sink;

            AddPostActivationAction(() =>
            {
                _device = DeviceManager.GetDeviceForKey(config.DeviceKey) as INvxDeviceWithHardware;
                if (_device == null)
                    throw new NullReferenceException(string.Format("Device at key: {0} is null.", config.DeviceKey));
            });

            AddPostActivationAction(() =>
            {
                RoutingOutputPort port = _device.OutputPorts[SwitcherForAnalogAudioOutput.Key];
                if (port == null)
                    throw new NullReferenceException("Audio output routing port is null.");

                TieLineCollection.Default.Add(new TieLine(port, sink.AudioIn));
            });

            AddPostActivationAction(() =>
            {
                Name = _device.Name;
                _audioName = new StringFeedback(() =>
                    string.IsNullOrEmpty(config.AudioName) ? _device.Name : config.AudioName);
                _audioName.FireUpdate();
            });

            AddPostActivationAction(() =>
            {
                StringFeedback feedback = _device.Feedbacks[CurrentSecondaryAudioStream.RouteNameKey] as StringFeedback;
                if (feedback == null)
                    throw new NullReferenceException(CurrentSecondaryAudioStream.RouteNameKey);

                StringFeedback audioSourceFeedback = _device.Feedbacks[AudioInputFeedback.Key] as StringFeedback;
                if (audioSourceFeedback == null)
                    throw new NullReferenceException(AudioInputFeedback.Key);

                _currentAudioRouteId = new IntFeedback(Key + "--appRouteAudioCurrentId", () =>
                {
                    if (AudioInputExtensions.AudioInputIsLocal(_device))
                    {
                        NvxApplicationAudioTransmitter self =
                            _transmitters.FirstOrDefault(t => t.Name.Equals(Name));
                        return self == null ? 0 : self.DeviceId;
                    }

                    if (feedback.StringValue.Equals(NvxGlobalRouter.NoSourceText))
                        return 0;

                    NvxApplicationAudioTransmitter result =
                        _transmitters.FirstOrDefault(t => t.Name.Equals(feedback.StringValue));
                    return result == null ? 0 : result.DeviceId;
                });

                feedback.OutputChange += (sender, args) => _currentAudioRouteId.FireUpdate();
                audioSourceFeedback.OutputChange += (sender, args) => _currentAudioRouteId.FireUpdate();
                _device.Feedbacks.Add(_currentAudioRouteId);
            });

            AddPostActivationAction(() =>
            {
                StringFeedback audioSourceFeedback = _device.Feedbacks[AudioInputFeedback.Key] as StringFeedback;
                if (audioSourceFeedback == null)
                    throw new NullReferenceException(AudioInputFeedback.Key);

                _currentAudioRouteName = new StringFeedback(Key + "--appRouteAudioName", () =>
                {
                    if (AudioInputExtensions.AudioInputIsLocal(_device))
                    {
                        NvxApplicationAudioTransmitter self = _transmitters.FirstOrDefault(t =>
                            t.DeviceId.Equals(_currentAudioRouteId.IntValue));
                        return self == null ? NvxGlobalRouter.NoSourceText : self.AudioName.StringValue;
                    }

                    if (_currentAudioRouteId.IntValue == 0)
                        return NvxGlobalRouter.NoSourceText;

                    NvxApplicationAudioTransmitter result =
                        _transmitters.FirstOrDefault(t => t.DeviceId.Equals(_currentAudioRouteId.IntValue));
                    return result == null ? NvxGlobalRouter.NoSourceText : result.AudioName.StringValue;
                });

                _currentAudioRouteId.OutputChange += (sender, args) => _currentAudioRouteName.FireUpdate();
                audioSourceFeedback.OutputChange += (sender, args) => _currentAudioRouteName.FireUpdate();
                _device.Feedbacks.Add(_currentAudioRouteName);
            });
        }
    }
}