using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Newtonsoft.Json;
using PepperDash.Essentials.DM;

namespace PepperDash.Essentials.Devices.Displays.SharpDisplay
{
    /// <summary>
    /// 
    /// </summary>
    public class SharpDisplay : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced, IDisposable
    {
        public IBasicCommunication Communication { get; private set; }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _monitor; }
        }

        private readonly SharpCommunicationMonitor _monitor;

        #region Command constants

        public const string InputPrefix = "INPS";
        public const string Hdmi1 = "2";
        public const string Hdmi2 = "3";
        public const string Hdmi3 = "4";
        public const string PowerPrefix = "POWR";
        public const string RspwPrefix = "RSPW";

        #endregion

        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }

        private CTimer _pollTimer;
        private bool _readyForCommands;
        private bool _readyForNextCommand = true;
        private readonly bool _tcpComm;
        private bool _PowerIsOn;
        private bool _IsWarmingUp;
        private bool _IsCoolingDown;
        private int _CurrentInputIndex;
        private ushort _RequestedPowerState; // 0:none 1:on 2:off
        private ushort _RequestedInputState; // 0:none 1-3:inputs 1-3 
        private eCommandType _lastCommandType;

        private readonly string videoMuteKey;
        private IHdmiBlanking _hdmiBlanking;
        private readonly SharpQueue _cmdQueue;
        private readonly SharpQueue _priorityQueue;
        private readonly CommunicationGather _PortGather;
        private RoutingInputPort _CurrentInputPort;
        private readonly CMutex _CommandMutex;
        private readonly CMutex _PowerMutex;

        protected override Func<bool> PowerIsOnFeedbackFunc
        {
            get { return () => _PowerIsOn; }
        }

        protected override Func<bool> IsWarmingUpFeedbackFunc
        {
            get { return () => _IsWarmingUp; }
        }

        protected override Func<bool> IsCoolingDownFeedbackFunc
        {
            get { return () => _IsCoolingDown; }
        }

        protected override Func<string> CurrentInputFeedbackFunc
        {
            get { return () => _CurrentInputPort.Key; }
        }


        /// <summary>
        /// Constructor for IBasicCommunication
        /// </summary>
        public SharpDisplay(string key, string name, IBasicCommunication comm,
            SharpDisplayPropertiesConfig config)
            : base(key, name)
        {
            Communication = comm;
            _PortGather = new CommunicationGather(Communication, "\r\n")
            {
                IncludeDelimiter = false
            };
            _PortGather.LineReceived += DelimitedTextReceived;
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironmentOnProgramStatusEventHandler;

            GenericTcpIpClient tcpComm = comm as GenericTcpIpClient;
            _readyForCommands = false;
            if (tcpComm != null)
            {
                _tcpComm = true;
                tcpComm.AutoReconnect = true;
                tcpComm.AutoReconnectIntervalMs = 10000;
                tcpComm.ConnectionChange += tcpComm_ConnectionChange;
            }
            else
            {
                _tcpComm = false;
            }

            _cmdQueue = new SharpQueue();
            _priorityQueue = new SharpQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            Input1Feedback = new BoolFeedback(() => _CurrentInputIndex == 1);
            Input2Feedback = new BoolFeedback(() => _CurrentInputIndex == 2);
            Input3Feedback = new BoolFeedback(() => _CurrentInputIndex == 3);

            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            WarmupTime = 10000;
            CooldownTime = 10000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

            _monitor = new SharpCommunicationMonitor(this, 120000, 300000);
            DeviceManager.AddDevice(_monitor);

            AddRoutingInputPort(new RoutingInputPort("HDMI 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), Hdmi1);

            AddRoutingInputPort(new RoutingInputPort("HDMI 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), Hdmi2);

            AddRoutingInputPort(new RoutingInputPort("HDMI 3", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi3), this), Hdmi3);

            if (config.VideoMuteKey != null)
            {
                videoMuteKey = config.VideoMuteKey;
            }
        }

        private void AddRoutingInputPort(RoutingInputPort port, string fbMatch)
        {
            port.FeedbackMatchObject = fbMatch;
            InputPorts.Add(port);
        }

        public override bool CustomActivate()
        {
            if (videoMuteKey != null)
            {
                IKeyed dev = DeviceManager.GetDeviceForKey(videoMuteKey);
                if (dev is DmRmcControllerBase)
                {
                    Debug.Console(0, this, "Using scaler {0} for video mute", videoMuteKey);
                    _hdmiBlanking = dev as DmRmcControllerBase;
                }
                else if (dev is NvxEpi.Abstractions.HdmiOutput.IHdmiOutput)
                {
                    Debug.Console(0, this, "Using nvx {0} for video mute", videoMuteKey);
                    _hdmiBlanking = dev as NvxEpi.Abstractions.HdmiOutput.IHdmiOutput;
                }
            }

            _pollTimer = new CTimer(o => StatusGet(), null, 0, 30000);

            Communication.Connect();
            if (!_tcpComm)
            {
                _readyForCommands = true;
            }

            _monitor.StatusChange += (o, a) =>
                Debug.Console(1, this, "Communication monitor state: {0}", _monitor.Status);
            _monitor.Start();
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LinkDisplayToApi(this, trilist, joinStart, joinMapKey, bridge);
            SharpDisplayJoinMap joinMap = new SharpDisplayJoinMap(joinStart);

            trilist.BooleanInput[joinMap.LampHoursSupported.JoinNumber].BoolValue = false;

            //Video Mute
            trilist.SetSigTrueAction(joinMap.VideoMuteOn.JoinNumber, VideoMuteOn);
            trilist.SetSigTrueAction(joinMap.VideoMuteOff.JoinNumber, VideoMuteOff);
            if (_hdmiBlanking != null)
            {
                trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = true;
                _hdmiBlanking.HdmiOutputBlankedFeedback.LinkInputSig(
                    trilist.BooleanInput[joinMap.VideoMuteOn.JoinNumber]);
            }

            IsWarmingUpFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Warming.JoinNumber]);
            IsCoolingDownFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Cooling.JoinNumber]);
            Input1Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 0]);
            Input2Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 1]);
            Input3Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 2]);
        }

        private void tcpComm_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (!e.Client.IsConnected)
            {
                _readyForCommands = false;
                _cmdQueue.ClearQueue();
                _priorityQueue.ClearQueue();
                _CurrentInputIndex = 0;
            }
            else
            {
                _readyForCommands = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DelimitedTextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            Debug.Console(1, this, "Received feedback: {0}", e.Text);
            try
            {
                switch (_lastCommandType)
                {
                    case eCommandType.PowerOff:
                        if (e.Text == "OK")
                        {
                            //Update power on feedback
                            if (_PowerIsOn)
                            {
                                _PowerIsOn = false;
                                PowerIsOnFeedback.FireUpdate();
                                VideoMuteOff();
                            }

                            //Clear power check
                            _PowerMutex.WaitForMutex();
                            if (_RequestedPowerState == 2)
                            {
                                _RequestedPowerState = 0;
                            }

                            _PowerMutex.ReleaseMutex();
                        }

                        break;
                    case eCommandType.PowerPoll:
                        ProcessPowerFb(e.Text);
                        break;
                    case eCommandType.InputPoll:
                        ProcessInputFb(e.Text);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }

            _lastCommandType = eCommandType.None;
            CrestronEnvironment.Sleep(100);
            _readyForNextCommand = true;
        }

        private void ProcessPowerFb(string powerFb)
        {
            uint powerFbInt = uint.Parse(powerFb);
            switch (powerFbInt)
            {
                case 1:
                {
                    _monitor.SetOnlineStatus(true);

                    //Update power on feedback
                    if (_PowerIsOn == false && !_IsCoolingDown && _RequestedPowerState != 2)
                    {
                        _PowerIsOn = true;
                        PowerIsOnFeedback.FireUpdate();
                    }

                    //Clear power check
                    _PowerMutex.WaitForMutex();
                    if (_RequestedPowerState == 1)
                    {
                        _RequestedPowerState = 0;
                    }

                    _PowerMutex.ReleaseMutex();
                    break;
                }
                case 0:
                {
                    //Update power on feedback
                    if (_PowerIsOn)
                    {
                        _PowerIsOn = false;
                        PowerIsOnFeedback.FireUpdate();
                        VideoMuteOff();
                    }

                    //Clear power check
                    _PowerMutex.WaitForMutex();
                    if (_RequestedPowerState == 2)
                    {
                        _RequestedPowerState = 0;
                    }

                    _PowerMutex.ReleaseMutex();
                    break;
                }
            }
        }

        private void ProcessInputFb(string inputFb)
        {
            try
            {
                Debug.Console(1, this, "Found valid input feedback reply: {0}", inputFb);
                uint inputFbInt = uint.Parse(inputFb);
                int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(inputFbInt.ToString()));
                RoutingInputPort newInput = InputPorts[index];
                if (_CurrentInputIndex != (index + 1))
                {
                    _CurrentInputIndex = index + 1; //Offset from 0 based index
                    Input1Feedback.FireUpdate();
                    Input2Feedback.FireUpdate();
                    Input3Feedback.FireUpdate();
                }

                if (newInput != null && newInput != _CurrentInputPort)
                {
                    _CurrentInputPort = newInput;
                    CurrentInputFeedback.FireUpdate();
                    OnSwitchChange(new RoutingNumericEventArgs(null, _CurrentInputPort, eRoutingSignalType.AudioVideo));
                }
            }
            catch
            {
                Debug.Console(1, this, "Invalid input feedback: {0}", inputFb);
            }
        }

        private void ResyncPowerOnState()
        {
            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendCommandRaw(eCommandType type, string cmd, bool priority)
        {
            if (_readyForCommands)
            {
                KeyValuePair<eCommandType, string> kvp = new KeyValuePair<eCommandType, string>(type, cmd);
                Debug.Console(1, this, "Enqueuing command: {0}", cmd);
                if (priority)
                {
                    _priorityQueue.AddOrUpdateCommand(kvp);
                }
                else
                {
                    _cmdQueue.AddOrUpdateCommand(kvp);
                }

                CrestronInvoke.BeginInvoke(o => ProcessQueue());
            }
            else
            {
                Debug.Console(1, this, "Sharp display not connected, ignoring command");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendCommand(eCommandType type, string cmdPrefix, string cmdValue, bool priority)
        {
            if (_readyForCommands)
            {
                string cmd = cmdPrefix + cmdValue.PadLeft(4, ' ') + "\r";
                KeyValuePair<eCommandType, string> kvp = new KeyValuePair<eCommandType, string>(type, cmd);
                Debug.Console(1, this, "Enqueuing command: {0}", cmd);
                if (priority)
                {
                    _priorityQueue.AddOrUpdateCommand(kvp);
                }
                else
                {
                    _cmdQueue.AddOrUpdateCommand(kvp);
                }

                CrestronInvoke.BeginInvoke(o => ProcessQueue());
            }
            else
            {
                Debug.Console(1, this, "Sharp display not connected, ignoring command");
            }
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (test)
            {
                //Pace the commands sending out
                while (_cmdQueue.Count > 0 || _priorityQueue.Count > 0)
                {
                    try
                    {
                        KeyValuePair<eCommandType, string> kvp = _priorityQueue.Count > 0
                            ? _priorityQueue.Dequeue()
                            : _cmdQueue.Dequeue();

                        if (kvp.Value != null)
                        {
                            Debug.Console(1, this, "Sending command: {0}", ComTextHelper.GetEscapedText(kvp.Value));
                            _readyForNextCommand = false;
                            _lastCommandType = kvp.Key;
                            Communication.SendText(kvp.Value);

                            if (kvp.Key == eCommandType.PowerOn)
                            {
                                Thread.Sleep(500);
                                _readyForNextCommand = true;
                            }

                            if (!Communication.IsConnected)
                            {
                                //Fail-safe for no feedback
                                Thread.Sleep(500);
                            }
                            else
                            {
                                int count = 0;
                                while (!_readyForNextCommand && count < 100)
                                {
                                    Thread.Sleep(20);
                                    count++;
                                }

                                if (count >= 100)
                                {
                                    Debug.Console(0, this,
                                        "ProcessQueue timed out waiting for next command. Last command: {0}",
                                        kvp.Value);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Caught an exception in ProcessQueue {0}\r{1}\r{2}", ex.Message,
                            ex.InnerException, ex.StackTrace);
                    }
                }

                _CommandMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void StatusGet()
        {
            if (_readyForCommands)
            {
                if (_PowerIsOn || _RequestedPowerState == 1)
                {
                    PowerGet();
                    InputGet();
                }
                else
                {
                    //Disable health monitoring while display is off
                    _monitor.SetOnlineStatus(true);
                }
            }
        }

        private void WarmupCallback(object o)
        {
            Debug.Console(1, this, "Warmup complete");
            WarmupDone();
        }

        private void WarmupStart()
        {
            if (!_IsWarmingUp)
            {
                CooldownTimer.Stop();
                _PowerIsOn = true;
                _IsWarmingUp = true;
                _IsCoolingDown = false;
                IsWarmingUpFeedback.FireUpdate();
                IsCoolingDownFeedback.FireUpdate();
                PowerIsOnFeedback.FireUpdate();
                WarmupTimer.Reset(WarmupTime);

                while (_IsWarmingUp)
                {
                    SendCommand(eCommandType.PowerPoll, PowerPrefix, "?", true);
                    ResyncPowerOnState();
                    Thread.Sleep(2000);
                }
            }
        }

        private void WarmupDone()
        {
            WarmupTimer.Stop();
            _IsCoolingDown = false;
            _IsWarmingUp = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

            PowerRspw();
            InputGet();

            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }

            ResyncPowerOnState();
            ProcessPower();

            //fail-safe for no feedback
            if (!_monitor.IsOnline)
            {
                _PowerMutex.WaitForMutex();
                _RequestedPowerState = 0;
                _PowerMutex.ReleaseMutex();
            }
        }

        private void CooldownCallback(object o)
        {
            Debug.Console(1, this, "Cooldown complete");
            CooldownDone();
        }

        private void CooldownStart()
        {
            if (!_IsCoolingDown)
            {
                WarmupTimer.Stop();
                _PowerIsOn = false;
                PowerIsOnFeedback.FireUpdate();
                _IsCoolingDown = true;
                _IsWarmingUp = false;
                IsWarmingUpFeedback.FireUpdate();
                IsCoolingDownFeedback.FireUpdate();
                CooldownTimer.Reset(CooldownTime);
            }
        }

        private void CooldownDone()
        {
            CooldownTimer.Stop();
            _IsWarmingUp = false;
            _IsCoolingDown = false;
            IsWarmingUpFeedback.FireUpdate();
            IsCoolingDownFeedback.FireUpdate();

            ProcessPower();

            //fail-safe for no feedback
            if (!_monitor.IsOnline)
            {
                _PowerMutex.WaitForMutex();
                _RequestedPowerState = 0;
                _PowerMutex.ReleaseMutex();
            }
        }

        private void PowerRspw()
        {
            SendCommand(eCommandType.Rspw, RspwPrefix, "1", false);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PowerOn()
        {
            _PowerMutex.WaitForMutex();
            _RequestedPowerState = 1;
            _PowerMutex.ReleaseMutex();
            ProcessPower();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PowerOff()
        {
            _PowerMutex.WaitForMutex();
            _RequestedPowerState = 2;
            _PowerMutex.ReleaseMutex();
            _RequestedInputState = 0;
            ProcessPower();
        }

        private void PowerOnGo()
        {
            SendCommand(eCommandType.PowerOn, PowerPrefix, "1", true);
            CrestronInvoke.BeginInvoke(o => WarmupStart());
        }

        private void PowerOffGo()
        {
            if (_hdmiBlanking != null)
            {
                _hdmiBlanking.UnblankOutput();
            }

            SendCommand(eCommandType.PowerOff, PowerPrefix, "0", true);
            CrestronInvoke.BeginInvoke(o => CooldownStart());
        }

        private void ProcessPower()
        {
            if (!_IsWarmingUp && !_IsCoolingDown)
            {
                if (_RequestedPowerState == 1 && (_PowerIsOn == false || !_monitor.IsOnline))
                {
                    PowerOnGo();
                }
                else if (_RequestedPowerState == 2 && (_PowerIsOn || !_monitor.IsOnline))
                {
                    PowerOffGo();
                }
            }
        }

        public override void PowerToggle()
        {
            if (_PowerIsOn)
            {
                PowerOff();
            }
            else
            {
                PowerOn();
            }
        }

        public void PowerGet()
        {
            SendCommand(eCommandType.PowerPoll, PowerPrefix, "?", false);
        }


        public void VideoMuteOn()
        {
            Debug.Console(1, "Video Mute On Requested");
            if (_hdmiBlanking != null)
            {
                _hdmiBlanking.BlankOutput();
            }
        }

        public void VideoMuteOff()
        {
            Debug.Console(1, "Video Mute Off Requested");
            if (_hdmiBlanking != null)
            {
                _hdmiBlanking.UnblankOutput();
            }
        }

        public void InputSelect(ushort input)
        {
            switch (input)
            {
                case 1:
                    InputHdmi1();
                    break;
                case 2:
                    InputHdmi2();
                    break;
                case 3:
                    InputHdmi3();
                    break;
            }
        }

        private void InputSelectGo(ushort input)
        {
            switch (input)
            {
                case 1:
                    InputHdmi1Go();
                    break;
                case 2:
                    InputHdmi2Go();
                    break;
                case 3:
                    InputHdmi3Go();
                    break;
            }

            _RequestedInputState = 0;
        }

        public void InputHdmi1()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputHdmi1Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 1;
            }
        }

        public void InputHdmi2()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputHdmi2Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 2;
            }
        }

        public void InputHdmi3()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputHdmi3Go();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = 3;
            }
        }

        private void InputHdmi1Go()
        {
            if (_CurrentInputIndex != 1)
            {
                SendCommand(eCommandType.Input, InputPrefix, Hdmi1, false);
                InputGet();
            }
        }

        public void InputHdmi2Go()
        {
            if (_CurrentInputIndex != 2)
            {
                SendCommand(eCommandType.Input, InputPrefix, Hdmi2, false);
                InputGet();
            }
        }

        public void InputHdmi3Go()
        {
            if (_CurrentInputIndex != 3)
            {
                SendCommand(eCommandType.Input, InputPrefix, Hdmi3, false);
                InputGet();
            }
        }

        public void InputGet()
        {
            SendCommand(eCommandType.InputPoll, InputPrefix, "?", false);
        }

        /// <summary>
        /// Executes a switch.
        /// </summary>
        /// <param name="selector"></param>
        public override void ExecuteSwitch(object selector)
        {
            ((Action)selector)();
        }

        public enum eCommandType
        {
            PowerOn,
            PowerOff,
            Input,
            PowerPoll,
            InputPoll,
            Rspw,
            None
        }

        private class SharpQueue
        {
            private readonly List<KeyValuePair<eCommandType, string>>
                Q = new List<KeyValuePair<eCommandType, string>>();

            public ushort Count
            {
                get { return (ushort)Q.Count; }
            }

            private readonly CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing Sharp Display commands
            /// </summary>
            public SharpQueue()
            {
            }

            public void AddOrUpdateCommand(KeyValuePair<eCommandType, string> command)
            {
                mutex.WaitForMutex();
                try
                {
                    int i = Q.FindIndex(x => x.Key.Equals(command.Key));
                    if (i != -1 && (command.Key == eCommandType.Input))
                    {
                        Q[i] = command;
                    }
                    else
                    {
                        Q.Add(command);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Sharp command queue add/update: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public void ClearQueue()
            {
                mutex.WaitForMutex();
                try
                {
                    Q.Clear();
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Sharp command queue clear: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public KeyValuePair<eCommandType, string> Dequeue()
            {
                KeyValuePair<eCommandType, string> kvp = new KeyValuePair<eCommandType, string>();
                mutex.WaitForMutex();
                try
                {
                    if (Q.Count > 0)
                    {
                        kvp = Q[0];
                        Q.RemoveAt(0);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in Sharp command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                return kvp;
            }
        }


        private void CrestronEnvironmentOnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
            Dispose();
        }

        public void Dispose()
        {
            if (_pollTimer != null) _pollTimer.Dispose();
            if (_CommandMutex != null) _CommandMutex.Dispose();
            if (_PowerMutex != null) _PowerMutex.Dispose();
        }
    }

    public class SharpDisplayJoinMap : DisplayControllerJoinMap
    {
        [JoinName("Warming")] public readonly JoinDataComplete Warming = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 53,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Warming"
            });

        [JoinName("Cooling")] public readonly JoinDataComplete Cooling = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 54,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Cooling"
            });

        [JoinName("Video Mute On")] public readonly JoinDataComplete VideoMuteOn = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 57,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Video Mute On"
            });

        [JoinName("Video Mute Off")] public readonly JoinDataComplete VideoMuteOff = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 58,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Video Mute Off"
            });

        [JoinName("Video Mute Supported")] public readonly JoinDataComplete VideoMuteSupported = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 55,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Video Mute Supported"
            });

        [JoinName("Lamp Hours Supported")] public readonly JoinDataComplete LampHoursSupported = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 56,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Lamp Hours Supported"
            });

        public SharpDisplayJoinMap(uint joinStart)
            : base(joinStart, typeof(SharpDisplayJoinMap))
        {
        }
    }

    public class SharpDisplayPropertiesConfig
    {
        [JsonProperty("videoMuteKey")] public string VideoMuteKey { get; set; }
    }

    public class SharpDisplayFactory : EssentialsDeviceFactory<SharpDisplay>
    {
        public SharpDisplayFactory()
        {
            TypeNames = new List<string>() { "sharpdisplay" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Sharp Display Device");

            SharpDisplayPropertiesConfig config = dc.Properties.ToObject<SharpDisplayPropertiesConfig>();
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new SharpDisplay(dc.Key, dc.Name, comm, config);
            return null;
        }
    }
}