using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.Devices.Common.LightwareUcx
{
    public class LightwareUcxDevice : EssentialsBridgeableDevice, IRoutingNumericWithFeedback, IDisposable
    {
        private const ushort maxInputs = 5;
        private const ushort maxVideoOutputs = 3;
        private const ushort maxAudioOutputs = 1;

        private readonly List<string> VideoInputs = new List<string>();
        private readonly List<string> VideoOutputs = new List<string>();
        private readonly List<string> AudioInputs = new List<string>();
        private readonly List<string> AudioOutputs = new List<string>();
        private readonly List<string> UsbInputs = new List<string>();
        private readonly List<string> UsbOutputs = new List<string>();
        public ISocketStatus Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }
        private readonly CrestronQueue<string> _commandQueue;
        private readonly CMutex _commandMutex;
        private readonly CTimer _commandTimer;
        private readonly CTimer _subscriptionTimer;
        private readonly CMutex _feedbackMutex;
        private bool _queueWaiting;
        private bool _commandReady = true;
        private bool configSent;
        private bool _usbAutoRouteFb;
        private int _requestedUsbRoute = -1;

        public Dictionary<uint, string> VideoInputNames { get; set; }
        public Dictionary<uint, string> VideoOutputNames { get; set; }
        public Dictionary<uint, string> AudioInputNames { get; set; }
        public Dictionary<uint, string> AudioOutputNames { get; set; }
        public Dictionary<uint, string> UsbInputNames { get; set; }

        private readonly bool[] _videoInputSyncFb = new bool[maxInputs];
        private readonly ushort[] _videoOutputRouteFb = new ushort[maxVideoOutputs];
        private readonly ushort[] _audioOutputRouteFb = new ushort[maxAudioOutputs];
        private readonly string[] _outputVideoRouteNameFb = new string[maxVideoOutputs];
        private readonly string[] _outputAudioRouteNameFb = new string[maxAudioOutputs];
        private readonly string[] _videoInputResolutionFb = new string[maxInputs];
        private readonly bool[] _outputConnectedFb = new bool[maxVideoOutputs];
        private readonly bool[] _usbConnectedFb = new bool[maxInputs];
        private readonly Dictionary<uint, bool> _outputMonitoringEnabled;
        private ushort _usbOutputRouteFb;
        private readonly IList<string> configSettings;

        public FeedbackCollection<BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> VideoOutputRouteFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> AudioOutputRouteFeedbacks { get; private set; }
        public IntFeedback UsbOutputRouteFeedback { get; private set; }
        public BoolFeedback UsbAutoRouteFeedback { get; private set; }
        public FeedbackCollection<StringFeedback> VideoInputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> AudioInputNameFeedbacks { get; private set; }

        public FeedbackCollection<StringFeedback> UsbInputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> VideoOutputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> AudioOutputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> VideoInputResolutionFeedbacks { get; private set; }
        public FeedbackCollection<BoolFeedback> OutputConnectedFeedbacks { get; private set; }
        public FeedbackCollection<BoolFeedback> UsbConnectedFeedbacks { get; private set; }
        public StringFeedback DeviceNameFeedback { get; private set; }

        public LightwareUcxDevice(string key, string name, ISocketStatus comm,
            LightwareUcxPropertiesConfig props)
            : base(key, name)
        {
            _commandQueue = new CrestronQueue<string>(500);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();
            _subscriptionTimer = new CTimer(subscriptionCallback, Timeout.Infinite);

            VideoInputNames = props.VideoInputNames ?? new Dictionary<uint, string>();
            AudioInputNames = props.AudioInputNames ?? new Dictionary<uint, string>();
            VideoOutputNames = props.VideoOutputNames ?? new Dictionary<uint, string>();
            AudioOutputNames = props.AudioOutputNames ?? new Dictionary<uint, string>();
            UsbInputNames = props.UsbInputNames ?? new Dictionary<uint, string>();

            configSettings = props.Config ?? new List<string>();
            _outputMonitoringEnabled = props.OutputMonitoringEnabled ?? new Dictionary<uint, bool>();

            DeviceNameFeedback = new StringFeedback(() => Name);
            VideoInputSyncFeedbacks = new FeedbackCollection<BoolFeedback>();
            VideoOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            AudioOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            VideoInputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            AudioInputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            UsbInputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            VideoOutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            AudioOutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputVideoRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputAudioRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
            VideoInputResolutionFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputConnectedFeedbacks = new FeedbackCollection<BoolFeedback>();
            UsbConnectedFeedbacks = new FeedbackCollection<BoolFeedback>();
            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            Communication = comm;
            Communication.ConnectionChange += CommunicationOnConnectionChange;
            CommunicationGather gather = new CommunicationGather(Communication, "\r\n");
            gather.LineReceived += GatherOnLineReceived;
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, Poll);
            DeviceManager.AddDevice(CommunicationMonitor);

            for (uint i = 0; i < maxInputs; i++)
            {
                uint index = i;
                VideoInputSyncFeedbacks.Add(new BoolFeedback(() => _videoInputSyncFb[index]));
                VideoInputNameFeedbacks.Add(new StringFeedback(() =>
                    VideoInputNames.ContainsKey(index + 1) && VideoInputNames[index + 1] != null
                        ? VideoInputNames[index + 1]
                        : ""));
                AudioInputNameFeedbacks.Add(new StringFeedback(() =>
                    AudioInputNames.ContainsKey(index + 1) && AudioInputNames[index + 1] != null
                        ? AudioInputNames[index + 1]
                        : ""));
                UsbInputNameFeedbacks.Add(new StringFeedback(() =>
                    UsbInputNames.ContainsKey(index + 1) && UsbInputNames[index + 1] != null
                        ? UsbInputNames[index + 1]
                        : ""));
                VideoInputResolutionFeedbacks.Add(new StringFeedback(() => _videoInputResolutionFb[index] == null
                    ? ""
                    : _videoInputResolutionFb[index]));
                UsbConnectedFeedbacks.Add(new BoolFeedback(() => _usbConnectedFb[index]));
            }

            for (uint i = 0; i < maxVideoOutputs; i++)
            {
                uint index = i;
                VideoOutputNameFeedbacks.Add(new StringFeedback(() =>
                    VideoOutputNames.ContainsKey(index + 1) && VideoOutputNames[index + 1] != null
                        ? VideoOutputNames[index + 1]
                        : ""));
                VideoOutputRouteFeedbacks.Add(new IntFeedback(() => _videoOutputRouteFb[index]));
                OutputVideoRouteNameFeedbacks.Add(new StringFeedback(() => _outputVideoRouteNameFb[index] == null
                    ? "None"
                    : _outputVideoRouteNameFb[index]));
                OutputConnectedFeedbacks.Add(new BoolFeedback(() => _outputConnectedFb[index]));
            }

            for (uint i = 0; i < maxAudioOutputs; i++)
            {
                uint index = i;
                AudioOutputNameFeedbacks.Add(new StringFeedback(() =>
                    AudioOutputNames.ContainsKey(index + 1) && AudioOutputNames[index + 1] != null
                        ? AudioOutputNames[index + 1]
                        : ""));
                AudioOutputRouteFeedbacks.Add(new IntFeedback(() => _audioOutputRouteFb[index]));
                OutputAudioRouteNameFeedbacks.Add(new StringFeedback(() => _outputAudioRouteNameFb[index] == null
                    ? "None"
                    : _outputAudioRouteNameFb[index]));
            }

            UsbOutputRouteFeedback = new IntFeedback(() => _usbOutputRouteFb);
            UsbAutoRouteFeedback = new BoolFeedback(() => _usbAutoRouteFb);
        }

        private void CommunicationOnConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (e.Client.IsConnected)
            {
                UpdateAllNodes();
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void UpdateAllNodes()
        {
            QueueCommand("GET /V1/MEDIA/VIDEO/XP");
            QueueCommand("GET /V1/MEDIA/AUDIO/XP");
            QueueCommand("GET /V1/MEDIA/USB/XP");
        }

        public override bool CustomActivate()
        {
            Debug.Console(1, this, "Starting comms");
            Communication.Connect();
            CommunicationMonitor.Start();
            return base.CustomActivate();
        }

        private void SendConfiguration()
        {
            foreach (string setting in configSettings)
            {
                QueueCommand(string.Format("SET {0}", setting));
            }

            configSent = true;
        }

        private void Subscribe()
        {
            if (VideoInputs.Count == 0)
            {
                UpdateAllNodes();
            }

            if (!configSent)
            {
                SendConfiguration();
            }

            foreach (string i in VideoInputs)
            {
                Debug.Console(1, this, "Subscribing video input {0}", i);
                QueueCommand(string.Format("OPEN /V1/MEDIA/VIDEO/{0}", i));
                QueueCommand(string.Format("GETALL /V1/MEDIA/VIDEO/{0}", i));
            }

            foreach (string o in VideoOutputs)
            {
                Debug.Console(1, this, "Subscribing video output {0}", o);
                QueueCommand(string.Format("OPEN /V1/MEDIA/VIDEO/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/VIDEO/XP/{0}", o));
            }

            foreach (string o in AudioOutputs)
            {
                Debug.Console(1, this, "Subscribing audio output {0}", o);
                QueueCommand(string.Format("OPEN /V1/MEDIA/AUDIO/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/AUDIO/XP/{0}", o));
            }

            foreach (string i in UsbInputs)
            {
                Debug.Console(1, this, "Subscribing usb input {0}", i);
                QueueCommand(string.Format("OPEN /V1/MEDIA/USB/XP/{0}", i));
                QueueCommand(string.Format("GETALL /V1/MEDIA/USB/XP/{0}", i));
            }

            foreach (string o in UsbOutputs)
            {
                Debug.Console(1, this, "Subscribing usb output {0}", o);
                QueueCommand(string.Format("OPEN /V1/MEDIA/USB/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/USB/XP/{0}", o));
                QueueCommand(string.Format("OPEN /V1/MEDIA/USB/AUTOSELECT/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/USB/AUTOSELECT/{0}", o));
            }
        }

        private void commandTimeout(object o)
        {
            Debug.Console(1, this, "Command timed out");
            _commandReady = true;
            ProcessQueue();
        }

        private void subscriptionCallback(object o)
        {
            Debug.Console(1, this, "Subscriptions lost, resubscribing");
            Subscribe();
        }

        protected void readyForNextCommand()
        {
            _commandTimer.Stop();
            _commandReady = true;
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            CrestronInvoke.BeginInvoke(o =>
            {
                //Thread safe queue processing below
                if (_queueWaiting == false)
                {
                    _queueWaiting = true;
                    if (_commandMutex.WaitForMutex())
                    {
                        Debug.Console(2, this, "Got command mutex");
                        _queueWaiting = false;
                        try
                        {
                            while (!_commandQueue.IsEmpty)
                            {
                                int count = 0;
                                while (!_commandReady && (count < 50))
                                {
                                    Thread.Sleep(100);
                                    count++;
                                }

                                string cmd = _commandQueue.TryToDequeue() + "\r\n";
                                _commandReady = false;
                                _commandTimer.Reset(200); //Wait maximum 200 ms for response

                                CrestronInvoke.BeginInvoke(obj => { Communication.SendText(cmd); });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Console(0, this, "Exception processing command queue: {0}", ex);
                        }
                        finally
                        {
                            _commandMutex.ReleaseMutex();
                            Debug.Console(2, this, "Released command mutex");
                        }
                    }
                    else
                    {
                        _queueWaiting = false;
                    }
                }
            });
        }

        public void QueueCommand(string cmd)
        {
            if (!_commandQueue.IsFull)
            {
                Debug.Console(2, this, "Queueing command: {0}", cmd);
                _commandQueue.TryToEnqueue(cmd);
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Command queue is full! Dropping command.");
                readyForNextCommand();
            }
        }

        public void RouteVideoInput(ushort input, ushort output)
        {
            QueueCommand(string.Format("CALL /V1/MEDIA/VIDEO/XP:switch({0}{1}:O{2})", input != 0 ? "I" : "", input,
                output));
        }

        public void RouteAudioInput(ushort input, ushort output)
        {
            if (output > 0 && output <= AudioOutputs.Count)
            {
                string outputString = AudioOutputs[output - 1];
                QueueCommand(string.Format("CALL /V1/MEDIA/AUDIO/XP:switch({0}{1}:{2})", input != 0 ? "I" : "", input,
                    outputString));
            }
        }

        private void ProcessUsbRoute()
        {
            if (_requestedUsbRoute != -1)
            {
                QueueCommand(string.Format("CALL /V1/MEDIA/USB/XP:switch({0}{1}:H1)",
                    _requestedUsbRoute != 0 ? "U" : "",
                    _requestedUsbRoute));
            }
        }

        public void RouteUsbInput(ushort input, ushort output)
        {
            if (output == 1)
            {
                if (_requestedUsbRoute == -1)
                {
                    _requestedUsbRoute = input;
                    ProcessUsbRoute();
                }
                else
                {
                    _requestedUsbRoute = input;
                }


                CrestronInvoke.BeginInvoke(o =>
                {
                    ushort count = 0;
                    //blink feedback while changing for up to ten seconds
                    while (_requestedUsbRoute == input && input > 0 && count < 5)
                    {
                        _usbOutputRouteFb = count % 2 == 0 ? input : (ushort)0;
                        UsbOutputRouteFeedback.FireUpdate();
                        CrestronEnvironment.Sleep(1000);
                        count++;
                    }
                });
            }
        }

        private void OnSwitchChange(ushort input, ushort output, eRoutingSignalType signalType)
        {
            RoutingNumericEventArgs e = new RoutingNumericEventArgs(output, input,
                null, null, signalType);

            if (NumericSwitchChange != null)
            {
                NumericSwitchChange(this, e);
            }
        }

        private void GatherOnLineReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            try
            {
                _feedbackMutex.WaitForMutex();
                processResponse(e.Text);
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning,
                    string.Format("Lightware exception parsing feedback: {0}, {1}", ex.Message, e.Text));
            }
            finally
            {
                _feedbackMutex.ReleaseMutex();
            }
        }

        private void processResponse(string response)
        {
            string[] responseArray = response.Split(' ');
            switch (responseArray[0])
            {
                case "CHG":
                    Debug.Console(2, this, "Change notification");
                    ProcessProperty(responseArray[1]);
                    break;
                case "o-":
                    //Subscription notification
                    Debug.Console(2, this, "Subscription notification");
                    _subscriptionTimer.Stop();
                    break;
                case "n-":
                    Debug.Console(2, this, "Node");
                    ProcessNode(responseArray[1]);
                    break;
                case "nE":
                    Debug.Console(2, this, "Error response for a node");
                    break;
                case "nm":
                    Debug.Console(2, this, "Manual for a node");
                    break;
                case "pr":
                    Debug.Console(2, this, "Read-only property");
                    ProcessProperty(responseArray[1]);
                    break;
                case "pw":
                    Debug.Console(2, this, "Read-write property");
                    ProcessProperty(responseArray[1]);
                    break;
                case "pE":
                    Debug.Console(2, this, "Error for the property");
                    break;
                case "pm":
                    Debug.Console(2, this, "Manual for the property");
                    break;
                case "m-":
                    Debug.Console(2, this, "Method");
                    break;
                case "mO":
                    Debug.Console(2, this, "Response after a successful method execution");
                    break;
                case "mF":
                    Debug.Console(2, this, "Response after a failed method execution");
                    break;
                case "mE":
                    Debug.Console(2, this, "Error for a method");
                    break;
                case "mm":
                    Debug.Console(2, this, "Manual for a method");
                    break;
                default:
                    Debug.Console(2, this, "Unknown response code");
                    break;
            }

            readyForNextCommand();
        }

        private void ProcessNode(string node)
        {
            // Split the path into segments
            string[] segments = node.Split('/');
            if (segments.Length == 6 && segments[1] == "V1" && segments[2] == "MEDIA" && segments[4] == "XP")
            {
                switch (segments[3])
                {
                    case "VIDEO":
                        if (segments[5].StartsWith("I") && !VideoInputs.Contains(segments[5]))
                        {
                            VideoInputs.Add(segments[5]);
                        }
                        else if (segments[5].StartsWith("O") && !VideoOutputs.Contains(segments[5]))
                        {
                            VideoOutputs.Add(segments[5]);
                        }

                        break;
                    case "AUDIO":
                        if (segments[5].StartsWith("I") && !AudioInputs.Contains(segments[5]))
                        {
                            AudioInputs.Add(segments[5]);
                        }
                        else if (segments[5].StartsWith("O") && !AudioOutputs.Contains(segments[5]))
                        {
                            AudioOutputs.Add(segments[5]);
                        }

                        break;
                    case "USB":
                        if (segments[5].StartsWith("U") && !UsbInputs.Contains(segments[5]))
                        {
                            UsbInputs.Add(segments[5]);
                        }
                        else if (segments[5].StartsWith("H") && !UsbOutputs.Contains(segments[5]))
                        {
                            UsbOutputs.Add(segments[5]);
                        }

                        break;
                }
            }
        }

        private void ProcessProperty(string path)
        {
            // Split the path into segments
            string[] segments = path.Split('/', '.', '=');
            switch (segments[1])
            {
                case "V1":
                    switch (segments[2])
                    {
                        case "MEDIA":
                        {
                            switch (segments[3])
                            {
                                case "VIDEO":
                                    ProcessVideoProperties(segments);
                                    break;
                                case "AUDIO":
                                    ProcessAudioProperties(segments);
                                    break;
                                case "USB":
                                    ProcessUsbProperties(segments);
                                    break;
                            }

                            break;
                        }
                    }

                    break;
            }
        }

        private void ProcessVideoProperties(string[] segments)
        {
            if (segments[4] == "XP")
            {
                if (segments[5].StartsWith("O") && segments[6] == "ConnectedSource")
                {
                    int output = VideoOutputs.IndexOf(segments[5]);
                    int input = VideoInputs.IndexOf(segments[7]) + 1;
                    if (output >= 0 && input >= 0)
                    {
                        _videoOutputRouteFb[output] = (ushort)input;
                        VideoOutputRouteFeedbacks[output].FireUpdate();
                        OutputVideoRouteNameFeedbacks[output].FireUpdate();
                        OnSwitchChange((ushort)input, (ushort)(output + 1), eRoutingSignalType.Video);
                    }
                }
                else if (segments[5].StartsWith("O") && segments[6] == "Connected")
                {
                    int output = VideoOutputs.IndexOf(segments[5]);
                    string state = segments[7];
                    if (output >= 0)
                    {
                        _outputConnectedFb[output] = state == "true";
                        OutputConnectedFeedbacks[output].FireUpdate();
                    }
                }
            }
            else
                switch (segments[5])
                {
                    case "SignalPresent":
                    {
                        int input = VideoInputs.IndexOf(segments[4]);
                        if (input >= 0)
                        {
                            _videoInputSyncFb[input] = segments[6] == "true";
                            VideoInputSyncFeedbacks[input].FireUpdate();
                        }

                        break;
                    }
                    case "ActiveResolution":
                    {
                        int input = VideoInputs.IndexOf(segments[4]);
                        if (input >= 0)
                        {
                            _videoInputResolutionFb[input] = segments[6];
                            VideoInputResolutionFeedbacks[input].FireUpdate();
                        }

                        break;
                    }
                }
        }

        private void ProcessAudioProperties(string[] segments)
        {
            switch (segments[4])
            {
                case "XP":
                    if (segments[5].StartsWith("O") && segments[6] == "ConnectedSource")
                    {
                        int output = AudioOutputs.IndexOf(segments[5]);
                        int input = AudioInputs.IndexOf(segments[7]) + 1;
                        if (output >= 0 && input >= 0)
                        {
                            _audioOutputRouteFb[output] = (ushort)input;
                            AudioOutputRouteFeedbacks[output].FireUpdate();
                            OutputAudioRouteNameFeedbacks[output].FireUpdate();
                            OnSwitchChange((ushort)input, (ushort)(output + 1), eRoutingSignalType.Audio);
                        }
                    }

                    break;
            }
        }

        private void ProcessUsbProperties(string[] segments)
        {
            Debug.Console(1, this, "Proccessing usb properties: {0} {1} {2}", segments[5], segments[6], segments[7]);
            switch (segments[4])
            {
                case "XP":
                    if (segments[5].StartsWith("H1") && segments[6] == "ConnectedSource")
                    {
                        int input = UsbInputs.IndexOf(segments[7]) + 1;
                        if (input >= 0)
                        {
                            _usbOutputRouteFb = (ushort)input;
                            UsbOutputRouteFeedback.FireUpdate();
                            OnSwitchChange((ushort)input, 1, eRoutingSignalType.UsbOutput);
                            if (_requestedUsbRoute == input)
                            {
                                _requestedUsbRoute = -1;
                            }
                            else
                            {
                                ProcessUsbRoute();
                            }
                        }
                    }
                    else if (segments[5].StartsWith("U") && segments[6] == "Connected")
                    {
                        int input = UsbInputs.IndexOf(segments[5]) + 1;
                        if (input >= 0)
                        {
                            _usbConnectedFb[input - 1] = segments[7] == "true";
                            Debug.Console(1, this, "Processing usb connected input: {0}", input,
                                _usbConnectedFb[input - 1]);
                            UsbConnectedFeedbacks[input - 1].FireUpdate();
                        }
                    }

                    break;

                case "AUTOSELECT":
                    if (segments[5] == "H1" && segments[6] == "Policy")
                    {
                        _usbAutoRouteFb = segments[7].ToLower().StartsWith("last");
                        UsbAutoRouteFeedback.FireUpdate();
                    }

                    break;
            }
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LightwareUcxJoinMap joinMap = new LightwareUcxJoinMap(joinStart);
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

            for (ushort i = 0; i < maxInputs; i++)
            {
                //Digital
                VideoInputSyncFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + i + 1]);
                UsbConnectedFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.UsbHostAvailable.JoinNumber + i + 1]);
                UsbAutoRouteFeedback.LinkInputSig(trilist.BooleanInput[joinMap.UsbAutoRouteToggle.JoinNumber]);
                trilist.SetSigTrueAction(joinMap.UsbAutoRouteToggle.JoinNumber, () => ToggleUsbAutoRoute());

                //Serial                
                VideoInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + i + 1]);
                VideoInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputVideoNames.JoinNumber + i + 1]);
                VideoInputResolutionFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputCurrentResolution.JoinNumber + i + 1]);
                AudioInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputAudioNames.JoinNumber + i + 1]);
                UsbInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputUsbNames.JoinNumber + i + 1]);
            }

            for (ushort i = 0; i < maxVideoOutputs; i++)
            {
                ushort output = (ushort)(i + 1);
                //Digital
                OutputConnectedFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.OutputVideoConnected.JoinNumber + output]);
                trilist.BooleanInput[joinMap.OutputMonitoringEnabled.JoinNumber + output].BoolValue =
                    _outputMonitoringEnabled.ContainsKey(output) && _outputMonitoringEnabled[output];

                //Analog
                VideoOutputRouteFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputVideo.JoinNumber + output]);
                trilist.SetUShortSigAction(joinMap.OutputVideo.JoinNumber + output,
                    a => ExecuteNumericSwitch(a, output, eRoutingSignalType.Video));

                //Serial
                VideoOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + output]);
                VideoOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputVideoNames.JoinNumber + output]);
                OutputVideoRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + output]);
            }

            for (ushort i = 0; i < maxAudioOutputs; i++)
            {
                ushort output = (ushort)(i + 1);
                //Analog
                AudioOutputRouteFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputAudio.JoinNumber + output]);
                trilist.SetUShortSigAction(joinMap.OutputAudio.JoinNumber + output,
                    a => ExecuteNumericSwitch(a, output, eRoutingSignalType.Audio));

                //Serial
                AudioOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputAudioNames.JoinNumber + output]);
                OutputAudioRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentAudioInputNames.JoinNumber + output]);
            }

            UsbOutputRouteFeedback.LinkInputSig(trilist.UShortInput[joinMap.OutputUsb.JoinNumber + 1]);
            trilist.SetUShortSigAction(joinMap.OutputUsb.JoinNumber + 1,
                a => ExecuteNumericSwitch(a, 1, eRoutingSignalType.UsbOutput));


            UpdateAllFeedbacks();
        }

        private void ToggleUsbAutoRoute()
        {
            if (_usbAutoRouteFb)
            {
                QueueCommand("SET /V1/MEDIA/USB/AUTOSELECT/H1.Policy=Off");
            }
            else
            {
                QueueCommand("SET /V1/MEDIA/USB/AUTOSELECT/H1.Policy=Last detect");
            }
        }

        private void UpdateAllFeedbacks()
        {
            DeviceNameFeedback.FireUpdate();
            UsbOutputRouteFeedback.FireUpdate();
            UsbAutoRouteFeedback.FireUpdate();

            foreach (StringFeedback feedback in VideoInputNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (BoolFeedback feedback in VideoInputSyncFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (IntFeedback feedback in VideoOutputRouteFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (IntFeedback feedback in AudioOutputRouteFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in VideoOutputNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in AudioInputNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in AudioOutputNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in UsbInputNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in OutputVideoRouteNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in OutputAudioRouteNameFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (StringFeedback feedback in VideoInputResolutionFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (BoolFeedback feedback in OutputConnectedFeedbacks)
            {
                feedback.FireUpdate();
            }

            foreach (BoolFeedback feedback in UsbConnectedFeedbacks)
            {
                feedback.FireUpdate();
            }
        }

        #endregion

        #region Poll

        public void Poll()
        {
            if (Communication.IsConnected)
            {
                _subscriptionTimer.Reset(5000);
                //Query open subscriptions
                QueueCommand("OPEN");
            }
        }

        #endregion

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            throw new NotImplementedException();
        }

        public void ExecuteNumericSwitch(ushort input, ushort output, eRoutingSignalType type)
        {
            Debug.Console(1, "Making numeric switch input:{0} output:{1} type:{2}", input, output, type);

            switch (type)
            {
                case eRoutingSignalType.Video:
                    RouteVideoInput(input, output);
                    break;
                case eRoutingSignalType.Audio:
                    RouteAudioInput(input, output);
                    break;
                case eRoutingSignalType.UsbOutput:
                    RouteUsbInput(input, output);
                    break;
            }
        }

        public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;

        public void Dispose()
        {
            if (_commandQueue != null) _commandQueue.Dispose();
            if (_commandMutex != null) _commandMutex.Dispose();
            if (_commandTimer != null) _commandTimer.Dispose();
            if (_feedbackMutex != null) _feedbackMutex.Dispose();
            if (_subscriptionTimer != null) _subscriptionTimer.Dispose();
        }
    }

    public class LightwareUcxFactory : EssentialsDeviceFactory<LightwareUcxDevice>
    {
        public LightwareUcxFactory()
        {
            TypeNames = new List<string> { "lightwareucx" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lightware Ucx device");

            ISocketStatus comms = CommFactory.CreateCommForDevice(dc) as ISocketStatus;
            if (comms == null)
            {
                Debug.ConsoleWithLog(0, "Lightware Ucx device needs to use a socket for comms");
                return null;
            }

            LightwareUcxPropertiesConfig config = dc.Properties.ToObject<LightwareUcxPropertiesConfig>();

            return new LightwareUcxDevice(dc.Key, dc.Name, comms, config);
        }
    }
}