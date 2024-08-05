using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.Devices.Common.ExtronDsc301
{
    public class ExtronDsc301Device : EssentialsBridgeableDevice
    {
        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }
        private CrestronQueue<string> _commandQueue;
        private CMutex _commandMutex;
        private CTimer _commandTimer;
        private CMutex _feedbackMutex;
        private byte[] _incomingBuffer = { };
        private bool _queueWaiting = false;
        private bool _commandReady = true;
        protected string _lastInquiry = "";

        private bool _autoSwitchFb;
        public bool AutoSwitchFb
        {
            get { return _autoSwitchFb; }
            set
            {
                if (_autoSwitchFb == value) return;
                _autoSwitchFb = value;

                CalculateInputFb();
            }
        }

        private ushort _rawInputFb;
        public ushort RawInputFb
        {
            get { return _rawInputFb; }
            set
            {
                if (_rawInputFb == value || _rawInputFb > 3) return;
                _rawInputFb = value;

                CalculateInputFb();
            }
        }

        private ushort _inputFb;
        public IntFeedback InputFb;

        private ushort _autoModeInputFb;
        public IntFeedback AutoModeInputFb;

        private bool _Input1Sync;
        public BoolFeedback Input1SyncFb;

        private bool _Input2Sync;
        public BoolFeedback Input2SyncFb;

        private bool _Input3Sync;
        public BoolFeedback Input3SyncFb;

        private string _Input0Name;
        public StringFeedback Input0NameFb;

        private string _Input1Name;
        public StringFeedback Input1NameFb;

        private string _Input2Name;
        public StringFeedback Input2NameFb;

        private string _Input3Name;
        public StringFeedback Input3NameFb;

        public ExtronDsc301Device(string key, string name, IBasicCommunication comm, ExtronDsc301PropertiesConfig config)
            : base(key, name)
        {
            _Input0Name = config.Input0Name != null ? config.Input0Name.ToString() : "";
            _Input1Name = config.Input1Name != null ? config.Input1Name.ToString() : "";
            _Input2Name = config.Input2Name != null ? config.Input2Name.ToString() : "";
            _Input3Name = config.Input3Name != null ? config.Input3Name.ToString() : "";

            _commandQueue = new CrestronQueue<string>(10);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();

            InputFb = new IntFeedback(() => _inputFb);
            AutoModeInputFb = new IntFeedback(() => _autoModeInputFb);
            Input1SyncFb = new BoolFeedback(() => _Input1Sync);
            Input2SyncFb = new BoolFeedback(() => _Input2Sync);
            Input3SyncFb = new BoolFeedback(() => _Input3Sync);
            Input0NameFb = new StringFeedback(() => _Input0Name);
            Input1NameFb = new StringFeedback(() => _Input1Name);
            Input2NameFb = new StringFeedback(() => _Input2Name);
            Input3NameFb = new StringFeedback(() => _Input3Name);

            Communication = comm;
            Communication.BytesReceived += Communication_BytesReceived;

            // Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat should be coming in every 30s if subscriptions are valid
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, Poll);
            CommunicationMonitor.StatusChange +=new EventHandler<MonitorStatusChangeEventArgs>(CommunicationMonitor_StatusChange);
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
                //Set Sync Timeout to 0s
                QueueCommand(0x1B + "T0SSAV");
                //Query HDCP Notification
                QueueCommand(0x1B + "NHDCP");
                //Get Auto Switch State
                QueueCommand(0x1B + "AUSW");
                //Get Current Input
                QueueCommand("!");
                //Get Video Sync
                QueueCommand(0x1B + "0LS");
                //Disable HDCP for HDMI
                QueueCommand(0x1B + "E3*0HDCP");
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void commandTimeout(object o)
        {
            Debug.Console(0, this, "Command timed out");
            _commandReady = true;
            ProcessQueue();
        }

        protected void readyForNextCommand()
        {
            _commandTimer.Stop(); //No need for timeout on last command
            _lastInquiry = "";
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
                                string cmd = _commandQueue.TryToDequeue();
                                _lastInquiry = cmd;
                                _commandReady = false;
                                _commandTimer.Reset(200); //Wait maximum 200 ms for response

                                CrestronInvoke.BeginInvoke((obj) =>
                                {
                                    Communication.SendText(cmd + "\r");
                                });
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
                Debug.Console(2, this, "Queueing command: {0}", ComTextHelper.GetDebugText(cmd));
                _commandQueue.TryToEnqueue(cmd);
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
                QueueCommand(string.Format("{0}!", input));
            }
        }

        public void AutoSwitchOn()
        {
            QueueCommand(0x1B + "1AUSW");
        }

        public void AutoSwitchOff()
        {
            QueueCommand(0x1B + "0AUSW");
        }

        private void EnableHdcpNotification()
        {
            QueueCommand(0x1B + "N1HDCP");
        }

        private void CalculateInputFb()
        {
            if (AutoSwitchFb)
            {
                _inputFb = 0;
                InputFb.FireUpdate();

                _autoModeInputFb = RawInputFb;
                AutoModeInputFb.FireUpdate();
            }
            else
            {
                _inputFb = RawInputFb;
                InputFb.FireUpdate();

                _autoModeInputFb = 0;
                AutoModeInputFb.FireUpdate();
            }
        }

        /// <summary>
        /// Communication bytes recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            try
            {
                _feedbackMutex.WaitForMutex();

                // Append the incoming bytes to whatever is in the buffer
                var newBytes = new byte[_incomingBuffer.Length + e.Bytes.Length];
                _incomingBuffer.CopyTo(newBytes, 0);
                e.Bytes.CopyTo(newBytes, _incomingBuffer.Length);

                // Look for CRLF and process when found
                int start = 0;
                for (int i = 1; i < newBytes.Length; i++)
                {
                    if (newBytes[i] == 0x0A && newBytes[i-1] == 0x0D)
                    {
                        var message = new byte[i - start - 1];

                        //Copy bytes to new array without the CRLF and then process
                        Array.Copy(newBytes, start, message, 0, i - start - 1);
                        start = i + 1;
                        CrestronInvoke.BeginInvoke((o) => processResponse(message));
                    }
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
                Debug.LogError(Debug.ErrorLogLevel.Warning, String.Format("ExtronDsc exception parsing feedback: {0}, {1}", ex.Message, ComTextHelper.GetEscapedText(_incomingBuffer)));
            }
            finally
            {
                _feedbackMutex.ReleaseMutex();
            }
        }

        private void processResponse(byte[] response)
        {
            string responseText = Encoding.GetEncoding(28591).GetString(response, 0, response.Length);
            Debug.Console(0, this, "Parsing: {0}, last inquiry: {1}", ComTextHelper.GetDebugText(responseText), _lastInquiry);
            try
            {
                if (responseText == "Reconfig")
                {
                    //Found new signal message
                    //Get Video Sync
                    QueueCommand(0x1B + "0LS");
                }
                else if (responseText.StartsWith("HplgO"))
                {
                    //Found hot plug message
                    Debug.Console(0, this, "Found hotplug event on output {0}", responseText.Substring(5,1));
                }
                else if (responseText.StartsWith("In") && responseText.EndsWith("All"))
                {
                    //Found route feedback message
                    RawInputFb = ushort.Parse(responseText.Substring(2, 1));
                    if(_lastInquiry.EndsWith("!"))
                    {
                        readyForNextCommand();
                    }
                }
                else if (responseText.StartsWith("SsavT"))
                {
                    //Found sync timeout message
                    Debug.Console(0, this, "Sync timeout set to {0}", responseText.Substring(5));
                    readyForNextCommand();
                }
                else
                {
                    switch (_lastInquiry)
                    {
                        case "\x1BNHDCP":
                            //Found HDCP notification response
                            readyForNextCommand();
                            break;
                        case "\x1BE3*0HDCP":
                            //Found HDCP disabled on HDMI response
                            readyForNextCommand();
                            break;
                        case "!":
                            //Found route feedback message
                            if(responseText.Length == 1)
                            {
                                RawInputFb = ushort.Parse(responseText);
                            }
                            Debug.Console(0, this, "Found route feedback {0}", RawInputFb);
                            readyForNextCommand();
                            break;
                        case "\x1BAUSW":
                            //Auto Switch Inquiry Reply
                            if(responseText.Length == 1)
                            {
                                if(responseText == "1" || responseText == "2")
                                {
                                    AutoSwitchFb = true;
                                }
                                else if(responseText == "0")
                                {
                                    AutoSwitchFb = false;
                                }
                                Debug.Console(0, this, "Found auto switch feedback {0}", AutoSwitchFb);
                            }
                            readyForNextCommand();
                            break;
                        case "\x1B0LS":
                            //Video Sync Reply
                            Debug.Console(0, this, "Found video sync feedback {0}", responseText);
                            {
                                if (responseText.Substring(1, 1) == "*" && responseText.Substring(3, 1) == "*" && responseText.Length == 5)
                                {
                                    _Input1Sync = responseText.Substring(0, 1) != "0";
                                    _Input2Sync = responseText.Substring(2, 1) != "0";
                                    _Input3Sync = responseText.Substring(4, 1) != "0";
                                    Input1SyncFb.FireUpdate();
                                    Input2SyncFb.FireUpdate();
                                    Input3SyncFb.FireUpdate();
                                }
                            }

                            readyForNextCommand();
                            break;
                        case "\x1B0AUSW":
                            //Auto Switch Disable Reply
                            if (responseText == "Ausw0")
                            {
                                AutoSwitchFb = false;
                                Debug.Console(0, this, "Found auto switch feedback {0}", AutoSwitchFb);
                            }
                            readyForNextCommand();
                            break;
                        case "\x1B1AUSW":
                            //Auto Switch Enable Reply
                            if (responseText == "Ausw1")
                            {
                                AutoSwitchFb = true;
                                Debug.Console(0, this, "Found auto switch feedback {0}", AutoSwitchFb);
                            }
                            readyForNextCommand();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(Debug.ErrorLogLevel.Warning, String.Format("ExtronDsc exception processing feedback: {0}, {1}", ex.Message, responseText));
                readyForNextCommand();
            }
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new DmTxControllerJoinMap(joinStart);
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            //Names
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
            Input0NameFb.LinkInputSig(trilist.StringInput[joinMap.Input0Name.JoinNumber]);
            Input1NameFb.LinkInputSig(trilist.StringInput[joinMap.Input1Name.JoinNumber]);
            Input2NameFb.LinkInputSig(trilist.StringInput[joinMap.Input2Name.JoinNumber]);
            Input3NameFb.LinkInputSig(trilist.StringInput[joinMap.Input3Name.JoinNumber]);

            //Video Sync
            Input1SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input1VideoSyncStatus.JoinNumber]);
            Input2SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input2VideoSyncStatus.JoinNumber]);
            Input3SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input3VideoSyncStatus.JoinNumber]);

            //Routing
            trilist.SetUShortSigAction(joinMap.VideoInput.JoinNumber, RouteInput);
            InputFb.LinkInputSig(trilist.UShortInput[joinMap.VideoInput.JoinNumber]);
            AutoModeInputFb.LinkInputSig(trilist.UShortInput[joinMap.AutoModeInput.JoinNumber]);
        }

        #endregion

        #region Poll

        public void Poll()
        {
            //Query HDCP Notification
            QueueCommand("\x1BNHDCP");
        }

        #endregion

    }

    public class ExtronDsc301PropertiesConfig
    {
        [JsonProperty("input0Name")]
        public string Input0Name { get; set; }

        [JsonProperty("input1Name")]
        public string Input1Name { get; set; }

        [JsonProperty("input2Name")]
        public string Input2Name { get; set; }

        [JsonProperty("input3Name")]
        public string Input3Name { get; set; }

        public ExtronDsc301PropertiesConfig()
        {
        }
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

            var comms = CommFactory.CreateCommForDevice(dc);
            var config = dc.Properties.ToObject<ExtronDsc301PropertiesConfig>();

            return new ExtronDsc301Device(dc.Key, dc.Name, comms, config);
        }
    }
}
