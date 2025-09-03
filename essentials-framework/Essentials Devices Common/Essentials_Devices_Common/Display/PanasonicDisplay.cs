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

namespace PepperDash.Essentials.Devices.Displays
{
    /// <summary>
    /// 
    /// </summary>
    public class PanasonicDisplay : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced
    {
        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #region Command constants

        public const string InputGetCmd = "\x02" + "QMI" + "\x03";
        public const string Hdmi1Cmd = "\x02" + "IMS:HM1" + "\x03";
        public const string Hdmi2Cmd = "\x02" + "IMS:HM2" + "\x03";
        public const string Pc1Cmd = "\x02" + "IMS:PC1" + "\x03";

        public const string PowerGetCmd = "\x02" + "QPW" + "\x03";
        public const string PowerOnCmd = "\x02" + "PON" + "\x03";
        public const string PowerOffCmd = "\x02" + "POF" + "\x03";

        public const string MuteOffCmd = "\x02" + "AMT:0" + "\x03";
        public const string MuteOnCmd = "\x02" + "AMT:1" + "\x03";
        public const string MuteGetCmd = "\x02" + "QAM" + "\x03";

        public const string VolumeGetCmd = "\x02" + "QAV" + "\x03";
        public const string VolumeLevelPartialCmd = "\x02" + "AVL:";
        public const string VolumeUpCmd = "\x02" + "AUU" + "\x03";
        public const string VolumeDownCmd = "\x02" + "AUD" + "\x03";

        public const string VideoMuteOffCmd = "\x02" + "VMT:0" + "\x03";
        public const string VideoMuteOnCmd = "\x02" + "VMT:1" + "\x03";
        public const string VideoMuteGetCmd = "\x02" + "QVM" + "\x03";

        #endregion

        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }
        public BoolFeedback VideoMuteIsOnFeedback { get; private set; }

        private readonly bool _supportsVideoMute;
        private bool _readyForCommands;
        private readonly bool _tcpComm;
        private bool _PowerIsOn;
        private bool _IsWarmingUp;
        private bool _IsCoolingDown;
        private int _CurrentInputIndex;
        private ushort _RequestedPowerState; // 0:none 1:on 2:off
        private ushort _RequestedInputState; // 0:none 1-3:inputs 1-3 
        private ushort _RequestedVideoMuteState; // 0:none 1:on 2:off
        private bool _VideoMuteIsOn;

        private readonly string videoMuteKey;
        private IHdmiBlanking _hdmiBlanking;
        private readonly PanasonicQueue _cmdQueue;
        private readonly PanasonicQueue _priorityQueue;
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
        public PanasonicDisplay(string key, string name, IBasicCommunication comm,
            PanasonicDisplayPropertiesConfig config)
            : base(key, name)
        {
            Communication = comm;
            _PortGather = new CommunicationGather(Communication, '\x03')
            {
                IncludeDelimiter = false
            };
            _PortGather.LineReceived += DelimitedTextReceived;

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

            _cmdQueue = new PanasonicQueue();
            _priorityQueue = new PanasonicQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            Input1Feedback = new BoolFeedback(() => _CurrentInputIndex == 1);
            Input2Feedback = new BoolFeedback(() => _CurrentInputIndex == 2);
            Input3Feedback = new BoolFeedback(() => _CurrentInputIndex == 3);
            VideoMuteIsOnFeedback = new BoolFeedback(() => _VideoMuteIsOn);

            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;
            _RequestedVideoMuteState = 0;
            WarmupTime = 15000;
            CooldownTime = 15000;
            WarmupTimer = new CTimer(WarmupCallback, Timeout.Infinite);
            CooldownTimer = new CTimer(CooldownCallback, Timeout.Infinite);

            CommunicationMonitor =
                new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);

            AddRoutingInputPort(new RoutingInputPort("HDMI 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi1), this), "HM1");

            AddRoutingInputPort(new RoutingInputPort("HDMI 2", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Hdmi, new Action(InputHdmi2), this), "HM2");

            AddRoutingInputPort(new RoutingInputPort("PC 1", eRoutingSignalType.Audio | eRoutingSignalType.Video,
                eRoutingPortConnectionType.Vga, new Action(InputPc1), this), "PC1");

            _supportsVideoMute = config.SupportsVideoMute;
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

            Communication.Connect();
            if (!_tcpComm)
            {
                _readyForCommands = true;
            }

            CommunicationMonitor.StatusChange += (o, a) =>
                Debug.Console(1, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
            CommunicationMonitor.Start();
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            LinkDisplayToApi(this, trilist, joinStart, joinMapKey, bridge);
            PanasonicDisplayJoinMap joinMap = new PanasonicDisplayJoinMap(joinStart);

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
            else if (_supportsVideoMute)
            {
                trilist.BooleanInput[joinMap.VideoMuteSupported.JoinNumber].BoolValue = true;
                VideoMuteIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.VideoMuteOn.JoinNumber]);
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
            try
            {
                if (e.Text.Contains("\x02"))
                {
                    int start = e.Text.IndexOf("\x02", StringComparison.Ordinal);
                    string fb = e.Text.Remove(0, start + 1); //Remove STX and any possible data before the STX
                    Debug.Console(1, this, "Received feedback: {0}", fb);
                    if (fb.StartsWith("QPW"))
                    {
                        ProcessPowerFb(fb);
                    }
                    else if (fb.StartsWith("QMI:"))
                    {
                        string inputFb = fb.Remove(0, 4);
                        if (inputFb.Length > 0)
                        {
                            if (inputFb.Length > 3)
                                inputFb = inputFb.Substring(0, 3);
                            ProcessInputFb(inputFb);
                        }
                    }
                    else if (fb.StartsWith("QVM:"))
                    {
                        ProcessVideoMuteFb(fb);
                    }
                }
                else
                {
                    Debug.Console(1, this, "Garbage feedback, discarding: {0}", e.Text);
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }
        }

        private void ProcessPowerFb(string powerFb)
        {
            if (powerFb == "QPW:1")
            {
                //Update power on feedback
                if (_PowerIsOn == false)
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

                //Finish the warming-up process
                if (_IsWarmingUp)
                {
                    CrestronInvoke.BeginInvoke((o) => WarmupDone());
                }
            }
            else if (powerFb == "QPW:0")
            {
                //Update power on feedback
                if (_PowerIsOn)
                {
                    _PowerIsOn = false;
                    _VideoMuteIsOn = false;
                    PowerIsOnFeedback.FireUpdate();
                    VideoMuteIsOnFeedback.FireUpdate();
                }

                //Clear power check
                _PowerMutex.WaitForMutex();
                if (_RequestedPowerState == 2)
                {
                    _RequestedPowerState = 0;
                }

                _PowerMutex.ReleaseMutex();

                //Finish the cooling-down process
                if (_IsCoolingDown)
                {
                    CrestronInvoke.BeginInvoke((o) => CooldownDone());
                }
            }
        }

        private void ProcessVideoMuteFb(string videoMuteFb)
        {
            switch (videoMuteFb)
            {
                case "QVM:1":
                {
                    _RequestedVideoMuteState = 0;
                    _VideoMuteIsOn = true;
                    VideoMuteIsOnFeedback.FireUpdate();
                    break;
                }
                case "QVM:0":
                {
                    _RequestedVideoMuteState = 0;
                    _VideoMuteIsOn = false;
                    VideoMuteIsOnFeedback.FireUpdate();
                    ResyncPowerOnState();
                    break;
                }
            }
        }

        private void ProcessInputFb(string inputFb)
        {
            try
            {
                Debug.Console(1, this, "Found valid input feedback reply: {0}", inputFb);
                int index = InputPorts.FindIndex(i => i.FeedbackMatchObject.Equals(inputFb));
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
            if (_RequestedVideoMuteState == 2)
            {
                VideoMuteOffGo();
            }

            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendCommand(eCommandType type, string cmd, bool priority)
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

                CrestronInvoke.BeginInvoke((o) => ProcessQueue());
            }
            else
            {
                Debug.Console(1, this, "Panasonic display not connected, ignoring command");
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
                            Communication.SendText(kvp.Value);
                            Thread.Sleep(500);
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
                PowerGet();
                InputGet();

                if (_hdmiBlanking == null && _supportsVideoMute)
                {
                    VideoMuteGet();
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
                    SendCommand(eCommandType.PowerPoll, PowerGetCmd, true);
                    if (_RequestedVideoMuteState == 1)
                    {
                        Debug.Console(1, "Sending video mute");
                        SendCommand(eCommandType.VideoMute, VideoMuteOnCmd, true);
                    }
                    else
                    {
                        ResyncPowerOnState();
                    }

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

            InputGet();

            if (_RequestedInputState != 0)
            {
                InputSelectGo(_RequestedInputState);
            }

            if (_hdmiBlanking == null)
            {
                VideoMuteGet();
            }

            if (_RequestedVideoMuteState == 1)
            {
                VideoMuteOnGo();
            }
            else
            {
                ResyncPowerOnState();
            }

            ProcessPower();

            //fail-safe for no feedback
            if (!CommunicationMonitor.IsOnline)
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
            _VideoMuteIsOn = false;
            VideoMuteIsOnFeedback.FireUpdate();

            ProcessPower();

            //fail-safe for no feedback
            if (!CommunicationMonitor.IsOnline)
            {
                _PowerMutex.WaitForMutex();
                _RequestedPowerState = 0;
                _PowerMutex.ReleaseMutex();
            }
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
            _RequestedVideoMuteState = 0;
            ProcessPower();
        }

        private void PowerOnGo()
        {
            SendCommand(eCommandType.Power, PowerOnCmd, true);
            CrestronInvoke.BeginInvoke((o) => WarmupStart());
        }

        private void PowerOffGo()
        {
            if (_hdmiBlanking != null)
            {
                _RequestedVideoMuteState = 0;
                _hdmiBlanking.UnblankOutput();
            }

            SendCommand(eCommandType.Power, PowerOffCmd, true);
            CrestronInvoke.BeginInvoke((o) => CooldownStart());
        }

        private void ProcessPower()
        {
            if (!_IsWarmingUp && !_IsCoolingDown)
            {
                if (_RequestedPowerState == 1 && (_PowerIsOn == false || !CommunicationMonitor.IsOnline))
                {
                    PowerOnGo();
                }
                else if (_RequestedPowerState == 2 && (_PowerIsOn || !CommunicationMonitor.IsOnline))
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
            SendCommand(eCommandType.PowerPoll, PowerGetCmd, false);
        }


        public void VideoMuteOn()
        {
            Debug.Console(1, "Video Mute On Requested");
            if (_hdmiBlanking != null)
            {
                _RequestedVideoMuteState = 0;
                _hdmiBlanking.BlankOutput();
            }
            else if (_supportsVideoMute && _RequestedPowerState == 1 || _PowerIsOn)
            {
                _RequestedVideoMuteState = 1;
                if (!_IsWarmingUp)
                {
                    VideoMuteOnGo();
                }
            }
        }

        private void VideoMuteOnGo()
        {
            if (_hdmiBlanking != null)
            {
                _RequestedVideoMuteState = 0;
                _hdmiBlanking.BlankOutput();
            }
            else if (_supportsVideoMute)
            {
                SendCommand(eCommandType.VideoMute, VideoMuteOnCmd, false);
                VideoMuteGet();
                CrestronInvoke.BeginInvoke((o) =>
                {
                    Thread.Sleep(1000);
                    VideoMuteGet();
                });
            }
        }

        public void VideoMuteOff()
        {
            Debug.Console(1, "Video Mute Off Requested");
            if (_hdmiBlanking != null)
            {
                _hdmiBlanking.UnblankOutput();
            }
            else if (_supportsVideoMute)
            {
                _RequestedVideoMuteState = 2;
                if (_PowerIsOn && !_IsWarmingUp)
                {
                    VideoMuteOffGo();
                }
            }
        }

        private void VideoMuteOffGo()
        {
            if (_hdmiBlanking != null)
            {
                _RequestedVideoMuteState = 0;
                _hdmiBlanking.UnblankOutput();
            }
            else if (_supportsVideoMute)
            {
                SendCommand(eCommandType.VideoMute, VideoMuteOffCmd, false);
                VideoMuteGet();
                CrestronInvoke.BeginInvoke((o) =>
                {
                    Thread.Sleep(1000);
                    VideoMuteGet();
                });
            }
        }

        public void VideoMuteGet()
        {
            if (_supportsVideoMute)
            {
                SendCommand(eCommandType.VideoMutePoll, VideoMuteGetCmd, false);
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
                    InputPc1();
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
                    InputPc1Go();
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

        public void InputPc1()
        {
            if (_PowerIsOn && !_IsWarmingUp)
            {
                _RequestedInputState = 0;
                InputPc1Go();
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
                SendCommand(eCommandType.Input, Hdmi1Cmd, false);
                InputGet();
            }
        }

        public void InputHdmi2Go()
        {
            if (_CurrentInputIndex != 2)
            {
                SendCommand(eCommandType.Input, Hdmi2Cmd, false);
                InputGet();
            }
        }

        public void InputPc1Go()
        {
            if (_CurrentInputIndex != 3)
            {
                SendCommand(eCommandType.Input, Pc1Cmd, false);
                InputGet();
            }
        }

        public void InputGet()
        {
            SendCommand(eCommandType.InputPoll, InputGetCmd, false);
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
            Power,
            Input,
            PowerPoll,
            InputPoll,
            VideoMute,
            VideoMutePoll
        }

        private class PanasonicQueue
        {
            private readonly List<KeyValuePair<eCommandType, string>>
                Q = new List<KeyValuePair<eCommandType, string>>();

            public ushort Count
            {
                get { return (ushort)Q.Count; }
            }

            private readonly CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing Panasonic Display commands
            /// </summary>
            public PanasonicQueue()
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
                    Debug.Console(1, "Exception in Panasonic command queue add/update: {0}", ex);
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
                    Debug.Console(1, "Exception in Panasonic command queue clear: {0}", ex);
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
                    Debug.Console(1, "Exception in Panasonic command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                return kvp;
            }
        }
    }

    public class PanasonicDisplayJoinMap : DisplayControllerJoinMap
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

        public PanasonicDisplayJoinMap(uint joinStart)
            : base(joinStart, typeof(PanasonicDisplayJoinMap))
        {
        }
    }

    public class PanasonicDisplayPropertiesConfig
    {
        [JsonProperty("videoMuteKey")] public string VideoMuteKey { get; set; }

        [JsonProperty("supportsVideoMute")] public bool SupportsVideoMute { get; set; }
    }

    public class PanasonicDisplayFactory : EssentialsDeviceFactory<PanasonicDisplay>
    {
        public PanasonicDisplayFactory()
        {
            TypeNames = new List<string>() { "panasonicdisplay" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Panasonic Display Device");

            PanasonicDisplayPropertiesConfig config = dc.Properties.ToObject<PanasonicDisplayPropertiesConfig>();
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new PanasonicDisplay(dc.Key, dc.Name, comm, config);
            return null;
        }
    }
}