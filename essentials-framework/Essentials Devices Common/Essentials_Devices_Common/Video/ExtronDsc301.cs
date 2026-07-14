using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using Newtonsoft.Json;
using Crestron.SimplSharpPro.DeviceSupport;
using UmdEssentials.Core;
using UmdEssentials.Core.Bridges;
using UmdEssentials.Core.Config;

namespace UmdEssentials.Devices.Common.ExtronDsc301
{
    public class ExtronDsc301Device : EssentialsBridgeableDevice, ITxRoutingWithFeedback
    {
        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }
        private readonly CrestronQueue<Dsc301Command> _commandQueue;
        private readonly CMutex _commandMutex;
        private readonly CTimer _commandTimer;
        private readonly CMutex _feedbackMutex;
        private byte[] _incomingBuffer = { };
        private bool _queueWaiting;
        private bool _commandReady = true;
        protected eDsc301Command _lastInquiry = eDsc301Command.NoFeedback;

        private bool _autoSwitchOnFb;

        public readonly BoolFeedback AutoSwitchOnFb;

        private ushort _inputFb;

        public ushort InputFb
        {
            get { return _inputFb; }
            set
            {
                if (_inputFb == value || _inputFb > 3) return;
                _inputFb = value;

                VideoSourceNumericFeedback.FireUpdate();
                AudioSourceNumericFeedback.FireUpdate();

                OnSwitchChange(_inputFb);
            }
        }


        private bool _Input1Sync;
        public readonly BoolFeedback Input1SyncFb;

        private bool _Input2Sync;
        public readonly BoolFeedback Input2SyncFb;

        private bool _Input3Sync;
        public readonly BoolFeedback Input3SyncFb;

        public readonly StringFeedback Input1NameFb;

        public readonly StringFeedback Input2NameFb;

        public readonly StringFeedback Input3NameFb;

        public ExtronDsc301Device(string key, string name, IBasicCommunication comm,
            ExtronDsc301PropertiesConfig config)
            : base(key, name)
        {
            string input1Name = config.Input1Name ?? "";
            string input2Name = config.Input2Name ?? "";
            string input3Name = config.Input3Name ?? "";

            _commandQueue = new CrestronQueue<Dsc301Command>(20);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();

            VideoSourceNumericFeedback = new IntFeedback(() => _inputFb);
            AudioSourceNumericFeedback = new IntFeedback(() => _inputFb);
            AutoSwitchOnFb = new BoolFeedback(() => _autoSwitchOnFb);
            Input1SyncFb = new BoolFeedback(() => _Input1Sync);
            Input2SyncFb = new BoolFeedback(() => _Input2Sync);
            Input3SyncFb = new BoolFeedback(() => _Input3Sync);
            Input1NameFb = new StringFeedback(() => input1Name);
            Input2NameFb = new StringFeedback(() => input2Name);
            Input3NameFb = new StringFeedback(() => input3Name);

            Communication = comm;
            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();
            Communication.BytesReceived += Communication_BytesReceived;

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 3000, 120000, 300000, Poll);
            CommunicationMonitor.StatusChange += CommunicationMonitor_StatusChange;
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return base.CustomActivate();
        }

        public void CommunicationMonitor_StatusChange(object o, MonitorStatusChangeEventArgs e)
        {
            if (e.Status == MonitorStatus.IsOk)
            {
                //Set Sync Timeout to 0 s
                QueueEscCommand(eDsc301Command.SetSyncTimeout, "T0SSAV\r");
                //Query HDCP Notification
                QueueEscCommand(eDsc301Command.HdcpNotification, "NHDCP\r");
                //Get Auto Switch State
                QueueEscCommand(eDsc301Command.GetAutoSwitch, "AUSW\r");
                //Get Current Input
                QueueCommand(eDsc301Command.Route, "!\r");
                //Get Video Sync
                QueueEscCommand(eDsc301Command.GetVideoSync, "0LS\r");
                //Disable HDCP for HDMI
                QueueEscCommand(eDsc301Command.SetHdcpOff, "E3*0HDCP\r");
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void commandTimeout(object o)
        {
            Debug.Console(1, this, "Command timed out");
            _commandReady = true;
            ProcessQueue();
        }

        protected void readyForNextCommand()
        {
            _commandTimer.Stop(); //No need for timeout on last command
            _lastInquiry = eDsc301Command.NoFeedback;
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
                                while (!_commandReady && count < 50)
                                {
                                    Thread.Sleep(100);
                                    count++;
                                }

                                Dsc301Command cmd = _commandQueue.TryToDequeue();
                                _lastInquiry = cmd.Command;
                                _commandReady = false;
                                _commandTimer.Reset(200); //Wait maximum 200 ms for response

                                CrestronInvoke.BeginInvoke((obj) => { Communication.SendBytes(cmd.Bytes); });
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

        public void QueueCommand(eDsc301Command inquiry, string cmd)
        {
            QueueCommand(inquiry, Encoding.GetEncoding(28591).GetBytes(cmd));
        }

        public void QueueEscCommand(eDsc301Command inquiry, string cmd)
        {
            byte[] cmdOut = new byte[cmd.Length + 1];
            cmdOut[0] = 0x1B;
            Encoding.GetEncoding(28591).GetBytes(cmd).CopyTo(cmdOut, 1);

            QueueCommand(inquiry, cmdOut);
        }

        public void QueueCommand(eDsc301Command inquiry, byte[] bytes)
        {
            if (!_commandQueue.IsFull)
            {
                Debug.Console(2, this, "Queueing command: {0}", ComTextHelper.GetEscapedText(bytes));
                _commandQueue.TryToEnqueue(new Dsc301Command(inquiry, bytes));
                ProcessQueue();
            }
            else
            {
                Debug.Console(0, this, "Command queue is full! Dropping command.");
                readyForNextCommand();
            }
        }

        public void RouteInput(ushort input)
        {
            if (input == 0)
            {
                AutoSwitchOn();
            }
            else
            {
                AutoSwitchOff();
                QueueCommand(eDsc301Command.Route, string.Format("{0}!\r", input));
            }
        }

        public void AutoSwitchOn()
        {
            QueueEscCommand(eDsc301Command.AutoSwitchOn, "1AUSW\r");
        }

        public void AutoSwitchOff()
        {
            QueueEscCommand(eDsc301Command.AutoSwitchOff, "0AUSW\r");
        }

        private void OnSwitchChange(ushort input)
        {
            RoutingNumericEventArgs e = new RoutingNumericEventArgs(1, input,
                null, null, eRoutingSignalType.AudioVideo);

            if (NumericSwitchChange != null) NumericSwitchChange(this, e);
        }

        /// <summary>
        /// Communication bytes received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            try
            {
                _feedbackMutex.WaitForMutex();

                // Append the incoming bytes to whatever is in the buffer
                byte[] newBytes = new byte[_incomingBuffer.Length + e.Bytes.Length];
                _incomingBuffer.CopyTo(newBytes, 0);
                e.Bytes.CopyTo(newBytes, _incomingBuffer.Length);

                // Look for CRLF and process when found
                int start = 0;
                for (int i = 1; i < newBytes.Length; i++)
                    if (newBytes[i] == 0x0A && newBytes[i - 1] == 0x0D)
                    {
                        byte[] message = new byte[i - start - 1];

                        //Copy bytes to a new array without the CRLF and then process
                        Array.Copy(newBytes, start, message, 0, i - start - 1);
                        start = i + 1;
                        CrestronInvoke.BeginInvoke((o) => processResponse(message));
                    }

                int extraDataLength = newBytes.Length - start;
                if (extraDataLength > 0 && extraDataLength < 30)
                {
                    // Copy data after last CRLF to new incoming buffer
                    _incomingBuffer = new byte[extraDataLength];
                    Array.Copy(newBytes, start, _incomingBuffer, 0, extraDataLength);
                }
                else
                {
                    _incomingBuffer = new byte[] { };
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning,
                    string.Format("ExtronDsc exception parsing feedback: {0}, {1}", ex.Message,
                        ComTextHelper.GetEscapedText(_incomingBuffer)));
            }
            finally
            {
                _feedbackMutex.ReleaseMutex();
            }
        }

        private void processResponse(byte[] response)
        {
            if (response.Length < 1)
                return;
            string responseText = Encoding.GetEncoding(28591).GetString(response, 0, response.Length);
            Debug.Console(2, this, "Parsing: {0}, last inquiry: {1}", ComTextHelper.GetDebugText(responseText),
                _lastInquiry);
            try
            {
                if (responseText == "Reconfig")
                {
                    //Found new signal message
                    //Get Video Sync
                    QueueEscCommand(eDsc301Command.GetVideoSync, "0LS\r");
                }
                else if (responseText.StartsWith("HplgO"))
                {
                    //Found the hot plug message
                    if (responseText.Length > 5)
                        Debug.Console(0, this, "Found hotplug event on output {0}", responseText.Substring(5, 1));
                }
                else if (responseText.StartsWith("In") && responseText.EndsWith("All"))
                {
                    //Found the route feedback message
                    InputFb = ushort.Parse(responseText.Substring(2, 1));
                    if (_lastInquiry == eDsc301Command.Route) readyForNextCommand();
                }
                else if (responseText.StartsWith("SsavT"))
                {
                    //Found sync timeout message
                    if (responseText.Length > 5)
                        Debug.Console(0, this, "Sync timeout set to {0}", responseText.Substring(5));

                    readyForNextCommand();
                }
                else
                {
                    switch (_lastInquiry)
                    {
                        case eDsc301Command.HdcpNotification:
                            //Found HDCP notification response
                            readyForNextCommand();
                            break;
                        case eDsc301Command.SetHdcpOff:
                            //Found HDCP disabled on HDMI response
                            readyForNextCommand();
                            break;
                        case eDsc301Command.Route:
                            //Found the route feedback message
                            if (responseText.Length == 1) InputFb = ushort.Parse(responseText);

                            Debug.Console(2, this, "Found route feedback {0}", InputFb);
                            readyForNextCommand();
                            break;
                        case eDsc301Command.GetAutoSwitch:
                            //Auto Switch Inquiry Reply
                            if (responseText.Length == 1)
                            {
                                switch (responseText)
                                {
                                    case "1":
                                    case "2":
                                        _autoSwitchOnFb = true;
                                        AutoSwitchOnFb.FireUpdate();
                                        break;
                                    case "0":
                                        _autoSwitchOnFb = false;
                                        AutoSwitchOnFb.FireUpdate();
                                        break;
                                }

                                Debug.Console(2, this, "Found auto switch feedback {0}", _autoSwitchOnFb);
                            }

                            readyForNextCommand();
                            break;
                        case eDsc301Command.GetVideoSync:
                            //Video Sync Reply
                            Debug.Console(1, this, "Found video sync feedback {0}", responseText);
                            if (responseText.Length == 5)
                                if (responseText.Substring(1, 1) == "*" && responseText.Substring(3, 1) == "*")
                                {
                                    _Input1Sync = responseText.Substring(0, 1) != "0";
                                    _Input2Sync = responseText.Substring(2, 1) != "0";
                                    _Input3Sync = responseText.Substring(4, 1) != "0";
                                    Input1SyncFb.FireUpdate();
                                    Input2SyncFb.FireUpdate();
                                    Input3SyncFb.FireUpdate();
                                }

                            readyForNextCommand();
                            break;
                        case eDsc301Command.AutoSwitchOff:
                            //Auto Switch Disable Reply
                            if (responseText == "Ausw0")
                            {
                                _autoSwitchOnFb = false;
                                AutoSwitchOnFb.FireUpdate();
                                Debug.Console(2, this, "Found auto switch feedback {0}", _autoSwitchOnFb);
                            }

                            readyForNextCommand();
                            break;
                        case eDsc301Command.AutoSwitchOn:
                            //Auto Switch Enable Reply
                            if (responseText == "Ausw1")
                            {
                                _autoSwitchOnFb = true;
                                AutoSwitchOnFb.FireUpdate();
                                Debug.Console(2, this, "Found auto switch feedback {0}", _autoSwitchOnFb);
                            }

                            readyForNextCommand();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning,
                    string.Format("ExtronDsc exception processing feedback: {0}, {1}", ex.Message, responseText));
                readyForNextCommand();
            }
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            DmTxControllerJoinMap joinMap = new DmTxControllerJoinMap(joinStart);
            if (bridge != null) bridge.AddJoinMap(Key, joinMap);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            //Names
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;
            Input1NameFb.LinkInputSig(trilist.StringInput[joinMap.Input1Name.JoinNumber]);
            Input2NameFb.LinkInputSig(trilist.StringInput[joinMap.Input2Name.JoinNumber]);
            Input3NameFb.LinkInputSig(trilist.StringInput[joinMap.Input3Name.JoinNumber]);

            //Video Sync
            Input1SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input1VideoSyncStatus.JoinNumber]);
            Input2SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input2VideoSyncStatus.JoinNumber]);
            Input3SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input3VideoSyncStatus.JoinNumber]);

            //Routing
            trilist.SetUShortSigAction(joinMap.VideoInput.JoinNumber, RouteInput);
            VideoSourceNumericFeedback.LinkInputSig(trilist.UShortInput[joinMap.VideoInput.JoinNumber]);

            trilist.SetSigTrueAction(joinMap.AutoModeOn.JoinNumber, AutoSwitchOn);
            trilist.SetSigTrueAction(joinMap.AutoModeOff.JoinNumber, AutoSwitchOff);
            AutoSwitchOnFb.LinkInputSig(trilist.BooleanInput[joinMap.AutoModeOn.JoinNumber]);
            AutoSwitchOnFb.LinkComplementInputSig(trilist.BooleanInput[joinMap.AutoModeOff.JoinNumber]);

            Input1NameFb.FireUpdate();
            Input2NameFb.FireUpdate();
            Input3NameFb.FireUpdate();
        }

        #endregion

        #region Poll

        public void Poll()
        {
            //Query HDCP Notification
            QueueEscCommand(eDsc301Command.GetVideoSync, "0LS\r");
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
            RouteInput(input);
        }

        public IntFeedback VideoSourceNumericFeedback { get; private set; }
        public IntFeedback AudioSourceNumericFeedback { get; private set; }
        public event EventHandler<RoutingNumericEventArgs> NumericSwitchChange;
    }

    public class Dsc301Command
    {
        public readonly eDsc301Command Command;
        public readonly byte[] Bytes;

        public Dsc301Command(eDsc301Command command, byte[] bytes)
        {
            Command = command;
            Bytes = bytes;
        }
    }

    /// <summary>
    /// For tracking feedback responses
    /// </summary>
    public enum eDsc301Command
    {
        Route,
        HdcpNotification,
        SetSyncTimeout,
        SetHdcpOff,
        GetVideoSync,
        GetAutoSwitch,
        AutoSwitchOn,
        AutoSwitchOff,
        NoFeedback
    }

    public class ExtronDsc301PropertiesConfig
    {
        [JsonProperty("input0Name")] public string Input0Name { get; set; }

        [JsonProperty("input1Name")] public string Input1Name { get; set; }

        [JsonProperty("input2Name")] public string Input2Name { get; set; }

        [JsonProperty("input3Name")] public string Input3Name { get; set; }
    }

    public class ExtronDsc301Factory : EssentialsDeviceFactory<ExtronDsc301Device>
    {
        public ExtronDsc301Factory()
        {
            TypeNames = new List<string> { "extrondsc301" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Extron Dsc301 device");

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
            ExtronDsc301PropertiesConfig config = dc.Properties.ToObject<ExtronDsc301PropertiesConfig>();

            return new ExtronDsc301Device(dc.Key, dc.Name, comms, config);
        }
    }
}