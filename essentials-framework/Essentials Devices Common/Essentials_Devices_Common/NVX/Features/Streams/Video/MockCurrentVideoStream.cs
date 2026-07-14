using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using NvxEpi.Abstractions.Stream;
using NvxEpi.Devices;
using NvxEpi.Features.Routing;
using PepperDash.Core;
using UmdEssentials.Core;

namespace NvxEpi.Features.Streams.Video
{
    public class MockCurrentVideoStream
    {
        public const string RouteNameKey = "CurrentVideoRoute";
        public const string RouteValueKey = "CurrentVideoRouteValue";
        private readonly IntFeedback _currentStreamId;
        private readonly StringFeedback _currentStreamName;
        private readonly CCriticalSection _lock = new CCriticalSection();
        private IStream _current;
        private readonly NvxMockDevice _device;

        private readonly IList<IStream> _transmitters = new List<IStream>();

        public MockCurrentVideoStream(NvxMockDevice device)
        {
            _device = device;
            _currentStreamId = device.IsTransmitter
                ? new IntFeedback(() => 0)
                : new IntFeedback(RouteValueKey, () => _current != null ? _current.DeviceId : 0);

            _currentStreamName = device.IsTransmitter
                ? new StringFeedback(() => string.Empty)
                : new StringFeedback(RouteNameKey,
                    () => _current != null ? _current.Name : NvxGlobalRouter.NoSourceText);

            device.Feedbacks.Add(_currentStreamId);
            device.Feedbacks.Add(_currentStreamName);

            Initialize();
        }

        public IntFeedback CurrentStreamId
        {
            get { return _currentStreamId; }
        }

        public StringFeedback CurrentStreamName
        {
            get { return _currentStreamName; }
        }

        public void UpdateCurrentRoute()
        {
            if (_device.IsTransmitter)
                return;

            try
            {
                _lock.Enter();
                _current = GetCurrentStream();

                if (_current == null)
                    Debug.Console(2, _device, "Current stream address: {0} device ID: {1}", "0.0.0.0", 0);
                else
                    Debug.Console(2, _device, "Current stream address: {0} device ID: {1}", _current.MulticastAddress,
                        _current.DeviceId);

                CurrentStreamId.FireUpdate();
                CurrentStreamName.FireUpdate();
                _device.StreamUrl.FireUpdate();
            }
            catch (Exception ex)
            {
                Debug.Console(1,
                    _device,
                    "Error getting current video route : {0}\r{1}\r{2}",
                    ex.Message,
                    ex.InnerException,
                    ex.StackTrace);
            }
            finally
            {
                _lock.Leave();
            }
        }

        private IStream GetCurrentStream()
        {
            if (string.IsNullOrEmpty(_device.StreamUrl.StringValue))
                return null;

            IStream result = _transmitters
                .Where(x => !string.IsNullOrEmpty(x.StreamUrl.StringValue))
                .FirstOrDefault(x => x.StreamUrl.StringValue.Equals(_device.StreamUrl.StringValue,
                    StringComparison.OrdinalIgnoreCase));

            if (result != null) return result;

            result = DeviceManager
                .AllDevices
                .OfType<IStream>()
                .Where(t => t.IsTransmitter)
                .Where(x => !string.IsNullOrEmpty(x.StreamUrl.StringValue))
                .FirstOrDefault(tx => tx.StreamUrl.StringValue.Equals(_device.StreamUrl.StringValue,
                    StringComparison.OrdinalIgnoreCase));

            if (result != null) _transmitters.Add(result);

            return result;
        }

        private void Initialize()
        {
            _device.IsOnline.OutputChange += (currentDevice, args) => UpdateCurrentRoute();
            _device.VideoStreamStatus.OutputChange += (sender, args) => UpdateCurrentRoute();
            _device.IsStreamingVideo.OutputChange += (currentDevice, args) => UpdateCurrentRoute();
            _device.MulticastAddress.OutputChange += (currentDevice, args) => UpdateCurrentRoute();
            _device.StreamUrl.OutputChange += (currentDevice, args) => UpdateCurrentRoute();
        }
    }
}