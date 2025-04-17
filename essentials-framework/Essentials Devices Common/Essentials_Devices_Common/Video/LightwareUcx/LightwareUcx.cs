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
        private const ushort maxOutputs = 4;

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

        public Dictionary<uint, string> VideoInputNames { get; set; }
        public Dictionary<uint, string> VideoOutputNames { get; set; }
        public Dictionary<uint, string> AudioInputNames { get; set; }
        public Dictionary<uint, string> AudioOutputNames { get; set; }

        private readonly bool[] _videoInputSyncFb = new bool[maxInputs];
        private readonly ushort[] _videoOutputRouteFb = new ushort[maxOutputs];
        private readonly ushort[] _audioOutputRouteFb = new ushort[maxOutputs];
        private readonly string[] _outputVideoRouteNameFb = new string[maxOutputs];
        private readonly string[] _outputAudioRouteNameFb = new string[maxOutputs];
        private ushort _usbOutputRouteFb;

        public FeedbackCollection<BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> VideoOutputRouteFeedbacks { get; private set; }
        public FeedbackCollection<IntFeedback> AudioOutputRouteFeedbacks { get; private set; }
        public IntFeedback UsbOutputRouteFeedback { get; private set; }
        public FeedbackCollection<StringFeedback> VideoInputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> AudioInputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> VideoOutputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> AudioOutputNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public FeedbackCollection<StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }
        public StringFeedback DeviceNameFeedback { get; private set; }

        public LightwareUcxDevice(string key, string name, ISocketStatus comm,
            LightwareUcxPropertiesConfig props)
            : base(key, name)
        {
            _commandQueue = new CrestronQueue<string>(50);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();
            _subscriptionTimer = new CTimer(subscriptionCallback, Timeout.Infinite);

            VideoInputNames = props.VideoInputNames ?? new Dictionary<uint, string>();
            AudioInputNames = props.AudioInputNames ?? new Dictionary<uint, string>();
            VideoOutputNames = props.VideoOutputNames ?? new Dictionary<uint, string>();
            AudioOutputNames = props.AudioOutputNames ?? new Dictionary<uint, string>();

            DeviceNameFeedback = new StringFeedback(() => Name);
            VideoInputSyncFeedbacks = new FeedbackCollection<BoolFeedback>();
            VideoOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            AudioOutputRouteFeedbacks = new FeedbackCollection<IntFeedback>();
            VideoInputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            AudioInputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            VideoOutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            AudioOutputNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputVideoRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
            OutputAudioRouteNameFeedbacks = new FeedbackCollection<StringFeedback>();
            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            Communication = comm;
            Communication.ConnectionChange += CommunicationOnConnectionChange;
            CommunicationGather gather = new CommunicationGather(Communication, "/r/n");
            gather.LineReceived += GatherOnLineReceived;
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, Poll);
            DeviceManager.AddDevice(CommunicationMonitor);

            for (uint i = 0; i < maxInputs; i++)
            {
                uint index = i;
                VideoInputSyncFeedbacks.Add(new BoolFeedback(() => _videoInputSyncFb[index]));
                VideoInputNameFeedbacks.Add(new StringFeedback(() =>
                    VideoInputNames.ContainsKey(index) && VideoInputNames[index] != null
                        ? VideoInputNames[index]
                        : ""));
                AudioInputNameFeedbacks.Add(new StringFeedback(() =>
                    AudioInputNames.ContainsKey(index) && AudioInputNames[index] != null
                        ? AudioInputNames[index]
                        : ""));
            }

            for (uint i = 0; i < maxOutputs; i++)
            {
                uint index = i;
                VideoOutputNameFeedbacks.Add(new StringFeedback(() =>
                    VideoOutputNames.ContainsKey(index) && VideoOutputNames[index] != null
                        ? VideoOutputNames[index]
                        : ""));
                VideoOutputRouteFeedbacks.Add(new IntFeedback(() => _videoOutputRouteFb[index]));
                OutputVideoRouteNameFeedbacks.Add(new StringFeedback(() => _outputVideoRouteNameFb[index] == null
                    ? "None"
                    : _outputVideoRouteNameFb[index]));
                AudioOutputNameFeedbacks.Add(new StringFeedback(() =>
                    AudioOutputNames.ContainsKey(index) && AudioOutputNames[index] != null
                        ? AudioOutputNames[index]
                        : ""));
                AudioOutputRouteFeedbacks.Add(new IntFeedback(() => _audioOutputRouteFb[index]));
                OutputAudioRouteNameFeedbacks.Add(new StringFeedback(() => _outputAudioRouteNameFb[index] == null
                    ? "None"
                    : _outputAudioRouteNameFb[index]));
            }

            UsbOutputRouteFeedback = new IntFeedback(() => _usbOutputRouteFb);
        }

        private void CommunicationOnConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (e.Client.IsConnected)
            {
                UpdateAllData();
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void UpdateAllData()
        {
            QueueCommand("GET /V1/MEDIA/VIDEO/XP");
            QueueCommand("GET /V1/MEDIA/AUDIO/XP");
            QueueCommand("GET /V1/MEDIA/USB/XP");
        }

        public override bool CustomActivate()
        {
            Debug.Console(0, this, "Starting comms");
            Communication.Connect();
            CommunicationMonitor.Start();
            return base.CustomActivate();
        }

        private void Subscribe()
        {
            foreach (string i in VideoInputs)
            {
                QueueCommand(string.Format("OPEN /V1/MEDIA/VIDEO/{0}", i));
                QueueCommand(string.Format("GETALL /V1/MEDIA/VIDEO/{0}", i));
            }

            foreach (string o in VideoOutputs)
            {
                QueueCommand(string.Format("OPEN /V1/MEDIA/VIDEO/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/VIDEO/XP/{0}", o));
            }

            foreach (string o in AudioOutputs)
            {
                QueueCommand(string.Format("OPEN /V1/MEDIA/AUDIO/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/AUDIO/XP/{0}", o));
            }

            foreach (string o in UsbOutputs)
            {
                QueueCommand(string.Format("OPEN /V1/MEDIA/USB/XP/{0}", o));
                QueueCommand(string.Format("GETALL /V1/MEDIA/USB/XP/{0}", o));
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
            CrestronInvoke.BeginInvoke((o) =>
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
                Debug.Console(0, this, "Queueing command: {0}", cmd);
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
            if (input <= VideoInputs.Count && output > 0 && output <= VideoOutputs.Count)
            {
                QueueCommand(string.Format("CALL /V1/MEDIA/VIDEO/XP:switch(I{0}:O{1})", input, output));
            }
        }

        public void RouteAudioInput(ushort input, ushort output)
        {
            if (input <= AudioInputs.Count && output > 0 && output <= AudioOutputs.Count)
            {
                QueueCommand(string.Format("CALL /V1/MEDIA/AUDIO/XP:switch(I{0}:O{1})", input, output));
            }
        }

        public void RouteUsbInput(ushort input, ushort output)
        {
            if (input <= UsbInputs.Count && output == 1)
            {
                QueueCommand(string.Format("CALL /V1/MEDIA/USB/XP:switch(U{0}:H1)", input));
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
            Debug.Console(0, this, "Parsing: {0}", response);

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
            }
            else if (segments[5] == "SignalPresent")
            {
                int input = VideoInputs.IndexOf(segments[4]);
                if (input >= 0)
                {
                    _videoInputSyncFb[input] = segments[6] == "true";
                    VideoInputSyncFeedbacks[input].FireUpdate();
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
            switch (segments[4])
            {
                case "XP":
                    if (segments[5].StartsWith("U") && segments[6] == "ConnectedSource")
                    {
                        int input = UsbInputs.IndexOf(segments[7]) + 1;
                        if (input >= 0)
                        {
                            _usbOutputRouteFb = (ushort)input;
                            UsbOutputRouteFeedback.FireUpdate();
                            OnSwitchChange((ushort)input, 1, eRoutingSignalType.UsbOutput);
                        }
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

            for (ushort i = 0; i <= VideoInputNames.Count; i++)
            {
                //Digital
                VideoInputSyncFeedbacks[i]
                    .LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + i]);

                //Serial                
                VideoInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + i]);
                VideoInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputVideoNames.JoinNumber + i]);
            }

            for (ushort i = 0; i <= VideoOutputNames.Count; i++)
            {
                ushort output = i;
                //Analog
                VideoOutputRouteFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputVideo.JoinNumber + i]);
                trilist.SetUShortSigAction(joinMap.OutputVideo.JoinNumber + i,
                    (a) => ExecuteNumericSwitch(a, output, eRoutingSignalType.Video));

                //Serial
                VideoOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + i]);
                VideoOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputVideoNames.JoinNumber + i]);
                OutputVideoRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + i]);
            }

            for (ushort i = 0; i <= AudioInputNames.Count; i++)
            {
                //Serial                
                AudioInputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.InputAudioNames.JoinNumber + i]);
            }

            for (ushort i = 0; i <= AudioOutputNames.Count; i++)
            {
                ushort output = i;
                //Analog
                AudioOutputRouteFeedbacks[i]
                    .LinkInputSig(trilist.UShortInput[joinMap.OutputAudio.JoinNumber + i]);
                trilist.SetUShortSigAction(joinMap.OutputAudio.JoinNumber + i,
                    (a) => ExecuteNumericSwitch(a, output, eRoutingSignalType.Audio));

                //Serial
                AudioOutputNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputAudioNames.JoinNumber + i]);
                OutputAudioRouteNameFeedbacks[i]
                    .LinkInputSig(trilist.StringInput[joinMap.OutputCurrentAudioInputNames.JoinNumber + i]);
            }

            UsbOutputRouteFeedback.LinkInputSig(trilist.UShortInput[joinMap.OutputUsb.JoinNumber + 1]);
            trilist.SetUShortSigAction(joinMap.OutputUsb.JoinNumber + 1,
                (a) => ExecuteNumericSwitch(a, 0, eRoutingSignalType.UsbOutput));
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