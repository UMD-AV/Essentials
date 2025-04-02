using System;
using System.Text;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.Devices.Common.LightwareUcx
{
    public class LightwareUcxDevice : EssentialsBridgeableDevice, ITxRoutingWithFeedback, IRoutingFeedback
    {
        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }
        private CrestronQueue<string> _commandQueue;
        private CMutex _commandMutex;
        private CTimer _commandTimer;
        private CMutex _feedbackMutex;
        private byte[] _incomingBuffer = { };
        private bool _queueWaiting;
        private bool _commandReady = true;
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
                if (_rawInputFb == value || _rawInputFb > 5) return;
                _rawInputFb = value;

                CalculateInputFb();
            }
        }

        private ushort _inputFb;

        private ushort _autoModeInputFb;
        public readonly IntFeedback AutoModeInputFb;

        private bool _Input1Sync;
        public readonly BoolFeedback Input1SyncFb;

        private bool _Input2Sync;
        public readonly BoolFeedback Input2SyncFb;

        private bool _Input3Sync;
        public readonly BoolFeedback Input3SyncFb;

        private bool _Input4Sync;
        public readonly BoolFeedback Input4SyncFb;

        private string _Input1VideoName;
        public StringFeedback Input1VideoNameFb;

        private string _Input2VideoName;
        public StringFeedback Input2VideoNameFb;

        private string _Input3VideoName;
        public StringFeedback Input3VideoNameFb;

        private string _Input4VideoName;
        public StringFeedback Input4VideoNameFb;

        private string _Input1UsbName;
        public StringFeedback Input1UsbNameFb;

        private string _Input2UsbName;
        public StringFeedback Input2UsbNameFb;

        private string _Input3UsbName;
        public StringFeedback Input3UsbNameFb;

        private string _Input4UsbName;
        public StringFeedback Input4UsbNameFb;

        public LightwareUcxDevice(string key, string name, IBasicCommunication comm,
            LightwareUcxPropertiesConfig config)
            : base(key, name)
        {
            _Input1VideoName = config.Input1VideoName ?? "";
            _Input2VideoName = config.Input2VideoName ?? "";
            _Input3VideoName = config.Input3VideoName ?? "";
            _Input4VideoName = config.Input4VideoName ?? "";

            _Input1UsbName = config.Input1UsbName ?? "";
            _Input2UsbName = config.Input2UsbName ?? "";
            _Input3UsbName = config.Input3UsbName ?? "";
            _Input4UsbName = config.Input4UsbName ?? "";

            _commandQueue = new CrestronQueue<string>(20);
            _commandMutex = new CMutex();
            _commandTimer = new CTimer(commandTimeout, Timeout.Infinite);
            _feedbackMutex = new CMutex();

            VideoSourceNumericFeedback = new IntFeedback(() => _inputFb);
            AudioSourceNumericFeedback = new IntFeedback(() => _inputFb);
            AutoModeInputFb = new IntFeedback(() => _autoModeInputFb);
            Input1SyncFb = new BoolFeedback(() => _Input1Sync);
            Input2SyncFb = new BoolFeedback(() => _Input2Sync);
            Input3SyncFb = new BoolFeedback(() => _Input3Sync);
            Input4SyncFb = new BoolFeedback(() => _Input4Sync);
            Input1VideoNameFb = new StringFeedback(() => _Input1VideoName);
            Input2VideoNameFb = new StringFeedback(() => _Input2VideoName);
            Input3VideoNameFb = new StringFeedback(() => _Input3VideoName);
            Input4VideoNameFb = new StringFeedback(() => _Input4VideoName);
            Input1UsbNameFb = new StringFeedback(() => _Input1UsbName);
            Input2UsbNameFb = new StringFeedback(() => _Input2UsbName);
            Input3UsbNameFb = new StringFeedback(() => _Input3UsbName);
            Input4UsbNameFb = new StringFeedback(() => _Input4UsbName);

            switch (config.Control.Method)
            {
                case eControlMethod.Wss:
                    Communication = new LightwareUcxWebSocket(key + "-websocket", config.Control);
                    break;
                case eControlMethod.Tcpip:
                    Communication = comm;
                    Communication.BytesReceived += Communication_BytesReceived;
                    break;
            }


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
                Subscribe();
            }
            else
            {
                _commandQueue.Clear();
            }
        }

        private void Subscribe()
        {
            QueueCommand("OPEN /V1/MEDIA/VIDEO/I1.SignalPresent");
            QueueCommand("OPEN /V1/MEDIA/VIDEO/I2.SignalPresent");
            QueueCommand("OPEN /V1/MEDIA/VIDEO/I3.SignalPresent");
            QueueCommand("OPEN /V1/MEDIA/VIDEO/I4.SignalPresent");
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

        public void RouteInput(ushort input)
        {
            if (input == 0)
            {
                AutoSwitchOn();
            }
            else
            {
                AutoSwitchOff();
                QueueCommand(string.Format("{0}!\r", input));
            }
        }

        public void AutoSwitchOn()
        {
            QueueCommand("SET /V1/MEDIA/VIDEO/AUTOSELECT/O1.Policy=Last Detect");
        }

        public void AutoSwitchOff()
        {
            QueueCommand("SET /V1/MEDIA/VIDEO/AUTOSELECT/O1.Policy=Off");
        }

        private void CalculateInputFb()
        {
            if (AutoSwitchFb)
            {
                _autoModeInputFb = RawInputFb;
                AutoModeInputFb.FireUpdate();

                _inputFb = 0;
                VideoSourceNumericFeedback.FireUpdate();
                AudioSourceNumericFeedback.FireUpdate();
            }
            else
            {
                _inputFb = RawInputFb;
                VideoSourceNumericFeedback.FireUpdate();
                AudioSourceNumericFeedback.FireUpdate();

                _autoModeInputFb = 0;
                AutoModeInputFb.FireUpdate();
            }

            OnSwitchChange(_inputFb);
        }

        private void OnSwitchChange(ushort input)
        {
            RoutingNumericEventArgs e = new RoutingNumericEventArgs(1, input,
                null, null, eRoutingSignalType.AudioVideo);

            if (NumericSwitchChange != null)
            {
                NumericSwitchChange(this, e);
            }
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
                {
                    if (newBytes[i] == 0x0A && newBytes[i - 1] == 0x0D)
                    {
                        byte[] message = new byte[i - start - 1];

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
            Debug.Console(0, this, "Parsing: {0}", ComTextHelper.GetDebugText(responseText));
            readyForNextCommand();
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

            //Names
            trilist.StringInput[joinMap.Name.JoinNumber].StringValue = this.Name;
            Input1VideoNameFb.LinkInputSig(trilist.StringInput[joinMap.Input1Name.JoinNumber]);
            Input2VideoNameFb.LinkInputSig(trilist.StringInput[joinMap.Input2Name.JoinNumber]);
            Input3VideoNameFb.LinkInputSig(trilist.StringInput[joinMap.Input3Name.JoinNumber]);
            Input4VideoNameFb.LinkInputSig(trilist.StringInput[joinMap.Input4Name.JoinNumber]);

            //Video Sync
            Input1SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input1VideoSyncStatus.JoinNumber]);
            Input2SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input2VideoSyncStatus.JoinNumber]);
            Input3SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input3VideoSyncStatus.JoinNumber]);
            Input4SyncFb.LinkInputSig(trilist.BooleanInput[joinMap.Input4VideoSyncStatus.JoinNumber]);

            //Routing
            trilist.SetUShortSigAction(joinMap.VideoInput.JoinNumber, RouteInput);
            VideoSourceNumericFeedback.LinkInputSig(trilist.UShortInput[joinMap.VideoInput.JoinNumber]);
            AutoModeInputFb.LinkInputSig(trilist.UShortInput[joinMap.AutoModeInput.JoinNumber]);

            Input1VideoNameFb.FireUpdate();
            Input2VideoNameFb.FireUpdate();
            Input3VideoNameFb.FireUpdate();
            Input4VideoNameFb.FireUpdate();
        }

        #endregion

        #region Poll

        public void Poll()
        {
            //Query HDCP Notification
            QueueCommand("OPEN");
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

    public class LightwareUcxPropertiesConfig
    {
        [JsonProperty("input1VideoName")] public string Input1VideoName { get; set; }
        [JsonProperty("input2VideoName")] public string Input2VideoName { get; set; }
        [JsonProperty("input3VideoName")] public string Input3VideoName { get; set; }
        [JsonProperty("input4VideoName")] public string Input4VideoName { get; set; }
        [JsonProperty("input1UsbName")] public string Input1UsbName { get; set; }
        [JsonProperty("input2UsbName")] public string Input2UsbName { get; set; }
        [JsonProperty("input3UsbName")] public string Input3UsbName { get; set; }
        [JsonProperty("input4UsbName")] public string Input4UsbName { get; set; }
        [JsonProperty("output1VideoName")] public string Output1VideoName { get; set; }
        [JsonProperty("output2VideoName")] public string Output2VideoName { get; set; }
        [JsonProperty("output3VideoName")] public string Output3VideoName { get; set; }

        [JsonProperty("control")] public ControlPropertiesConfig Control { get; set; }
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

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);
            LightwareUcxPropertiesConfig config = dc.Properties.ToObject<LightwareUcxPropertiesConfig>();

            return new LightwareUcxDevice(dc.Key, dc.Name, comms, config);
        }
    }

    public class LightwareUcxJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Online", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital
            });

        [JoinName("VideoSyncStatus")] public JoinDataComplete VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Video Sync", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("FreeRunEnabled")] public JoinDataComplete FreeRunEnabled = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Enable Free Run Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Input1VideoSyncStatus")] public JoinDataComplete Input1VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Input 1 Video Sync Status", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Input2VideoSyncStatus")] public JoinDataComplete Input2VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Input 2 Video Sync Status", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Input3VideoSyncStatus")] public JoinDataComplete Input3VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Input 3 Video Sync Status", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Input4VideoSyncStatus")] public JoinDataComplete Input4VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 7, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Input 4 Video Sync Status", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("CurrentInputResolution")] public JoinDataComplete CurrentInputResolution = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Current Input Resolution", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial
            });

        [JoinName("Input0Name")] public JoinDataComplete Input0Name = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Input 0 (Auto Switch) Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Input1Name")] public JoinDataComplete Input1Name = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Input 1 Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Input2Name")] public JoinDataComplete Input2Name = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Input 2 Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Input3Name")] public JoinDataComplete Input3Name = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Input 3 Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Input4Name")] public JoinDataComplete Input4Name = new JoinDataComplete(
            new JoinData { JoinNumber = 7, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Input 4 Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("VideoInput")] public JoinDataComplete VideoInput = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Video Input Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("AudioInput")] public JoinDataComplete AudioInput = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Audio Input Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("AutoModeInput")] public JoinDataComplete AutoModeInput = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX Auto Mode Input Get", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("HdcpSupportCapability")] public JoinDataComplete HdcpSupportCapability = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "DM TX HDCP Support Capability", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public LightwareUcxJoinMap(uint joinStart)
            : this(joinStart, typeof(LightwareUcxJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected LightwareUcxJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}