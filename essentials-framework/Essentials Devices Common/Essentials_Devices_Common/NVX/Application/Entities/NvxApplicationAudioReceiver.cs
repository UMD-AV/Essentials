using System;
using System.Collections.Generic;
using System.Linq;
using NvxEpi.Abstractions;
using NvxEpi.Application.Config;
using NvxEpi.Extensions;
using NvxEpi.Features.Routing;
using NvxEpi.Features.Streams.Audio;
using NvxEpi.Services.Feedback;
using NvxEpi.Services.InputSwitching;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common;

namespace NvxEpi.Application.Entities
{
    public class NvxApplicationAudioReceiver : EssentialsDevice
    {
        private readonly IRoutingSink _amp;
        private readonly int _deviceId;
        private readonly IEnumerable<NvxApplicationAudioTransmitter> _transmitters;

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
                Device = DeviceManager.GetDeviceForKey(config.DeviceKey) as INvxDeviceWithHardware;
                if (Device == null)
                    throw new NullReferenceException(string.Format("Device at key: {0} is null.", config.DeviceKey));
            });

            AddPostActivationAction(() =>
            {
                RoutingOutputPort port = Device.OutputPorts[SwitcherForAnalogAudioOutput.Key];
                if (port == null)
                    throw new NullReferenceException("Audio output routing port is null.");

                TieLineCollection.Default.Add(new TieLine(port, sink.AudioIn));
            });

            AddPostActivationAction(() =>
            {
                Name = Device.Name;
                AudioName = new StringFeedback(() =>
                    string.IsNullOrEmpty(config.AudioName) ? Device.Name : config.AudioName);
                AudioName.FireUpdate();
            });

            AddPostActivationAction(() =>
            {
                StringFeedback feedback = Device.Feedbacks[CurrentSecondaryAudioStream.RouteNameKey] as StringFeedback;
                if (feedback == null)
                    throw new NullReferenceException(CurrentSecondaryAudioStream.RouteNameKey);

                StringFeedback audioSourceFeedback = Device.Feedbacks[AudioInputFeedback.Key] as StringFeedback;
                if (audioSourceFeedback == null)
                    throw new NullReferenceException(AudioInputFeedback.Key);

                CurrentAudioRouteId = new IntFeedback(Key + "--appRouteAudioCurrentId", () =>
                {
                    if (AudioInputExtensions.AudioInputIsLocal(Device))
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

                feedback.OutputChange += (sender, args) => CurrentAudioRouteId.FireUpdate();
                audioSourceFeedback.OutputChange += (sender, args) => CurrentAudioRouteId.FireUpdate();
                Device.Feedbacks.Add(CurrentAudioRouteId);
            });

            AddPostActivationAction(() =>
            {
                StringFeedback audioSourceFeedback = Device.Feedbacks[AudioInputFeedback.Key] as StringFeedback;
                if (audioSourceFeedback == null)
                    throw new NullReferenceException(AudioInputFeedback.Key);

                CurrentAudioRouteName = new StringFeedback(Key + "--appRouteAudioName", () =>
                {
                    if (AudioInputExtensions.AudioInputIsLocal(Device))
                    {
                        NvxApplicationAudioTransmitter self = _transmitters.FirstOrDefault(t =>
                            t.DeviceId.Equals(CurrentAudioRouteId.IntValue));
                        return self == null ? NvxGlobalRouter.NoSourceText : self.AudioName.StringValue;
                    }

                    if (CurrentAudioRouteId.IntValue == 0)
                        return NvxGlobalRouter.NoSourceText;

                    NvxApplicationAudioTransmitter result =
                        _transmitters.FirstOrDefault(t => t.DeviceId.Equals(CurrentAudioRouteId.IntValue));
                    return result == null ? NvxGlobalRouter.NoSourceText : result.AudioName.StringValue;
                });

                CurrentAudioRouteId.OutputChange += (sender, args) => CurrentAudioRouteName.FireUpdate();
                audioSourceFeedback.OutputChange += (sender, args) => CurrentAudioRouteName.FireUpdate();
                Device.Feedbacks.Add(CurrentAudioRouteName);
            });
        }

        public int DeviceId
        {
            get { return _deviceId; }
        }

        public INvxDeviceWithHardware Device { get; private set; }

        public IRoutingSink Amp
        {
            get { return _amp; }
        }

        public StringFeedback AudioName { get; private set; }

        public StringFeedback CurrentAudioRouteName { get; private set; }

        public IntFeedback CurrentAudioRouteId { get; private set; }
    }
}