using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.DM.Streaming;
using NvxEpi.Abstractions.SecondaryAudio;
using NvxEpi.Abstractions.Stream;
using NvxEpi.Enums;
using NvxEpi.Extensions;
using NvxEpi.Features.Config;
using NvxEpi.Features.Streams.Video;
using NvxEpi.JoinMaps;
using NvxEpi.Services.Bridge;
using NvxEpi.Services.Feedback;
using NvxEpi.Services.InputSwitching;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace NvxEpi.Devices
{
    public class NvxMockDevice : EssentialsDevice, ISecondaryAudioStream, IRoutingNumeric, IBridgeAdvanced, IStream
    {
        private MockCurrentVideoStream _currentVideoStream;

        private readonly RoutingPortCollection<RoutingInputPort> _inputPorts =
            new RoutingPortCollection<RoutingInputPort>();

        private readonly RoutingPortCollection<RoutingOutputPort> _outputPorts =
            new RoutingPortCollection<RoutingOutputPort>();

        private string _streamUrl;

        public NvxMockDevice(DeviceConfig dc)
            : base(dc.Key, dc.Name)
        {
            NvxMockDeviceProperties props = dc.Properties.ToObject<NvxMockDeviceProperties>();
            Feedbacks = new FeedbackCollection<Feedback>();
            DeviceId = props.DeviceId;
            IsTransmitter = props.Mode == "tx";
            _streamUrl = !string.IsNullOrEmpty(props.StreamUrl) ? props.StreamUrl : string.Empty;
            BuildFeedbacks(props);
            BuildInputPorts();
        }

        private void BuildFeedbacks(NvxMockDeviceProperties props)
        {
            IsOnline = new BoolFeedback("IsOnline", () => true);
            DeviceMode =
                new IntFeedback(() => IsTransmitter ? (int)eDeviceMode.Transmitter : (int)eDeviceMode.Receiver);
            StreamUrl = new StringFeedback("StreamUrl", () => _streamUrl);

            MulticastAddress = new StringFeedback("MulticastVideoAddress",
                () => !string.IsNullOrEmpty(props.MulticastVideoAddress) ? props.MulticastVideoAddress : string.Empty);

            IsStreamingVideo = new BoolFeedback(() => !string.IsNullOrEmpty(props.StreamUrl));

            VideoStreamStatus = new StringFeedback(
                () => !string.IsNullOrEmpty(props.StreamUrl) ? "Streaming" : string.Empty);

            CurrentVideoStream = new StringFeedback(
                () => _currentVideoStream.CurrentStreamName.StringValue);

            SecondaryAudioAddress = new StringFeedback(
                () => !string.IsNullOrEmpty(props.MulticastAudioAddress) ? props.MulticastAudioAddress : string.Empty);

            TxAudioAddress = new StringFeedback("MulticastAudio",
                () => !string.IsNullOrEmpty(props.MulticastAudioAddress) ? props.MulticastAudioAddress : string.Empty);

            RxAudioAddress = new StringFeedback(() => string.Empty);

            IsStreamingSecondaryAudio = new BoolFeedback(
                () => !string.IsNullOrEmpty(props.MulticastAudioAddress));

            SecondaryAudioStreamStatus = new StringFeedback(
                () => !string.IsNullOrEmpty(props.MulticastAudioAddress) ? "Streaming" : string.Empty);

            Feedbacks.AddRange(new Feedback[]
            {
                DeviceNameFeedback.GetFeedback(Name),
                IsOnline,
                StreamUrl,
                MulticastAddress,
                TxAudioAddress,
                CurrentVideoStream
            });
        }

        private void BuildInputPorts()
        {
            if (IsTransmitter)
            {
                InputPorts.Add(
                    new RoutingInputPort(
                        DeviceInputEnum.NoSwitch.Name,
                        eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Hdmi,
                        DeviceInputEnum.NoSwitch,
                        this));

                InputPorts.Add(
                    new RoutingInputPort(
                        DeviceInputEnum.SecondaryAudio.Name,
                        eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.Streaming,
                        DeviceInputEnum.SecondaryAudio,
                        this));

                OutputPorts.Add(
                    new RoutingOutputPort(
                        SwitcherForStreamOutput.Key,
                        eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Streaming,
                        null,
                        this));

                OutputPorts.Add(
                    new RoutingOutputPort(
                        SwitcherForSecondaryAudioOutput.Key,
                        eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.LineAudio,
                        null,
                        this));
            }
            else
            {
                InputPorts.Add(
                    new RoutingInputPort(
                        DeviceInputEnum.Stream.Name,
                        eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Streaming,
                        DeviceInputEnum.Stream,
                        this));

                InputPorts.Add(
                    new RoutingInputPort(
                        DeviceInputEnum.SecondaryAudio.Name,
                        eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.Streaming,
                        DeviceInputEnum.SecondaryAudio,
                        this));

                OutputPorts.Add(
                    new RoutingOutputPort(
                        SwitcherForHdmiOutput.Key,
                        eRoutingSignalType.AudioVideo,
                        eRoutingPortConnectionType.Hdmi,
                        null,
                        this));

                OutputPorts.Add(
                    new RoutingOutputPort(
                        SwitcherForSecondaryAudioOutput.Key,
                        eRoutingSignalType.Audio,
                        eRoutingPortConnectionType.LineAudio,
                        null,
                        this));
            }
        }

        public override bool CustomActivate()
        {
            _currentVideoStream = new MockCurrentVideoStream(this);

            Feedbacks.ToList().ForEach(x => x.FireUpdate());

            return base.CustomActivate();
        }

        public RoutingPortCollection<RoutingInputPort> InputPorts
        {
            get { return _inputPorts; }
        }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { return _outputPorts; }
        }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            Debug.Console(0, this, "Executing switch : {0}", signalType);
        }

        public void ExecuteNumericSwitch(ushort input, ushort output, eRoutingSignalType type)
        {
            Debug.Console(0, this, "Executing switch : {0}, {1}, {2}", input, output, type);
        }

        public FeedbackCollection<Feedback> Feedbacks { get; private set; }
        public BoolFeedback IsOnline { get; private set; }
        public IntFeedback DeviceMode { get; private set; }
        public bool IsTransmitter { get; private set; }
        public int DeviceId { get; private set; }
        public StringFeedback StreamUrl { get; private set; }
        public StringFeedback SecondaryAudioAddress { get; private set; }
        public StringFeedback TxAudioAddress { get; private set; }
        public StringFeedback RxAudioAddress { get; private set; }

        public bool IsMock
        {
            get { return true; }
        }

        public BoolFeedback IsStreamingVideo { get; private set; }
        public StringFeedback VideoStreamStatus { get; private set; }
        public StringFeedback CurrentVideoStream { get; private set; }
        public BoolFeedback IsStreamingSecondaryAudio { get; private set; }
        public StringFeedback SecondaryAudioStreamStatus { get; private set; }
        public StringFeedback MulticastAddress { get; private set; }

        public StringFeedback CurrentStreamName
        {
            get { return _currentVideoStream.CurrentStreamName; }
        }

        public IntFeedback CurrentStreamId
        {
            get { return _currentVideoStream.CurrentStreamId; }
        }

        public void ClearStreamMock()
        {
            SetStreamUrlMock("");
        }

        public void SetStreamUrlMock(string url)
        {
            if (url == "")
            {
                url = this.Key;
            }

            if (url.Equals(_streamUrl))
                return;

            string oldUrl = _streamUrl;
            _streamUrl = url;
            StreamUrl.FireUpdate();

            if (IsTransmitter)
            {
                foreach (IStreamWithHardware rx in DeviceManager.AllDevices.OfType<IStreamWithHardware>()
                             .Where(x => !x.IsTransmitter && x.StreamUrl.StringValue.Equals(oldUrl)))
                {
                    rx.RouteStream(this);
                }
            }
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            NvxDeviceJoinMap joinMap = new NvxDeviceJoinMap(joinStart);

            NvxDeviceBridge deviceBridge = new NvxDeviceBridge(this);
            deviceBridge.LinkToApi(trilist, joinStart, joinMapKey, bridge);
            trilist.SetStringSigAction(joinMap.StreamUrl.JoinNumber, SetStreamUrlMock);
        }
    }
}