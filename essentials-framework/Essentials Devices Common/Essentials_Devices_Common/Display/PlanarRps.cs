using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Newtonsoft.Json;

namespace PepperDash.Essentials.Devices.Displays
{
    /// <summary>
    /// 
    /// </summary>
    public class PlanarRps : TwoWayDisplayBase, ICommunicationMonitor, IBridgeAdvanced
    {
        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        #region Command constants

        public const string AlertCheckOpCode = "ALERT.CHECK";

        public const string PresetActiveOpCode = "PRESET.ACTIVE";
        public const string PresetRecallOpCode = "PRESET.RECALL";

        public const string PowerOpCode = "SYSTEM.POWER";

        #endregion

        public BoolFeedback Input1Feedback { get; private set; }
        public BoolFeedback Input2Feedback { get; private set; }
        public BoolFeedback Input3Feedback { get; private set; }
        public BoolFeedback Input4Feedback { get; private set; }

        private bool _readyForCommands;
        private readonly bool _tcpComm;
        private bool _PowerIsOn;
        private int _CurrentInputIndex;
        private ushort _RequestedPowerState; // 0:none 1:on 2:off
        private ushort _RequestedInputState; // 0:none 1-4:inputs 1-4 

        private readonly PlanarQueue _cmdQueue;
        private readonly CommunicationGather _PortGather;
        private readonly CMutex _CommandMutex;
        private readonly CMutex _PowerMutex;


        /// <summary>
        /// Constructor for IBasicCommunication
        /// </summary>
        public PlanarRps(string key, string name, IBasicCommunication comm)
            : base(key, name)
        {
            Communication = comm;
            _PortGather = new CommunicationGather(Communication, '\x0D')
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

            _cmdQueue = new PlanarQueue();
            _CommandMutex = new CMutex();
            _PowerMutex = new CMutex();

            Input1Feedback = new BoolFeedback(() => _CurrentInputIndex == 1);
            Input2Feedback = new BoolFeedback(() => _CurrentInputIndex == 2);
            Input3Feedback = new BoolFeedback(() => _CurrentInputIndex == 3);
            Input4Feedback = new BoolFeedback(() => _CurrentInputIndex == 4);

            _CurrentInputIndex = 0;
            _RequestedPowerState = 0;
            _RequestedInputState = 0;

            CommunicationMonitor =
                new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, StatusGet, true);
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
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
            DisplayControllerJoinMap joinMap = new DisplayControllerJoinMap(joinStart);

            Input1Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 0]);
            Input2Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 1]);
            Input3Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 2]);
            Input4Feedback.LinkInputSig(trilist.BooleanInput[joinMap.InputSelectOffset.JoinNumber + 3]);
        }

        private void tcpComm_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            if (!e.Client.IsConnected)
            {
                _readyForCommands = false;
                _cmdQueue.ClearQueue();
                _CurrentInputIndex = 0;
            }
            else
            {
                _readyForCommands = true;
            }
        }

        private void DelimitedTextReceived(object sender, GenericCommMethodReceiveTextArgs e)
        {
            try
            {
                Debug.Console(1, this, "Feedback: {0}", e.Text);
                string[] response = e.Text.Split(':');

                if (response.Length <= 1) return;
                switch (response[0])
                {
                    case PowerOpCode:
                        ProcessPowerFb(response[1]);
                        break;
                    case PresetActiveOpCode:
                        ProcessPresetFb(response[1]);
                        break;
                    case PresetRecallOpCode:
                        break;
                    case AlertCheckOpCode:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, this, "Error parsing feedback: {0}", ex);
            }
        }

        private void ProcessPowerFb(string powerFb)
        {
            switch (powerFb)
            {
                case "ON":
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
                    break;
                }
                case "OFF":
                {
                    //Update power on feedback
                    if (_PowerIsOn)
                    {
                        _PowerIsOn = false;
                        PowerIsOnFeedback.FireUpdate();
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

        private void ProcessPresetFb(string presetFb)
        {
            try
            {
                int preset = int.Parse(presetFb);
                _CurrentInputIndex = preset;
                if (_CurrentInputIndex == _RequestedInputState)
                {
                    _RequestedInputState = 0;
                }

                Input1Feedback.FireUpdate();
                Input2Feedback.FireUpdate();
                Input3Feedback.FireUpdate();
                Input4Feedback.FireUpdate();
            }
            catch
            {
                Debug.Console(1, this, "Planar rps preset feedback not a number");
            }
        }

        private void SendCommand(string cmd)
        {
            if (_readyForCommands)
            {
                Debug.Console(1, this, "Enqueuing command: {0}", cmd);
                _cmdQueue.AddOrUpdateCommand(cmd);

                CrestronInvoke.BeginInvoke((o) => ProcessQueue());
            }
            else
            {
                Debug.Console(1, this, "Planar rps not connected, ignoring command");
            }
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (!test) return;

            //Pace the commands sending out
            while (_cmdQueue.Count > 0)
            {
                try
                {
                    string cmd = _cmdQueue.Dequeue();

                    if (cmd == null) continue;
                    Debug.Console(1, this, "Sending text: {0}", cmd);
                    Communication.SendText(cmd + "\r");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Debug.Console(0, this, "Caught an exception in ProcessQueue {0}\r{1}\r{2}", ex.Message,
                        ex.InnerException, ex.StackTrace);
                }
            }

            _CommandMutex.ReleaseMutex();
        }

        private void StatusGet()
        {
            if (!_readyForCommands) return;
            PowerGet();
            InputGet();
        }

        protected override Func<string> CurrentInputFeedbackFunc
        {
            get { return () => string.Format("Preset {0}", _CurrentInputIndex); }
        }

        protected override Func<bool> PowerIsOnFeedbackFunc
        {
            get { return () => _PowerIsOn; }
        }

        protected override Func<bool> IsCoolingDownFeedbackFunc
        {
            get { return () => false; }
        }

        protected override Func<bool> IsWarmingUpFeedbackFunc
        {
            get { return () => false; }
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
            SendCommand(string.Format("{0}=ON", PowerOpCode));
        }

        private void PowerOffGo()
        {
            SendCommand(string.Format("{0}=OFF", PowerOpCode));
        }

        private void ProcessPower()
        {
            if (_RequestedPowerState == 1 && (!_PowerIsOn || !CommunicationMonitor.IsOnline))
            {
                PowerOnGo();
            }
            else if (_RequestedPowerState == 2 && (_PowerIsOn || !CommunicationMonitor.IsOnline))
            {
                PowerOffGo();
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

        public override void ExecuteSwitch(object selector)
        {
        }

        public void PowerGet()
        {
            SendCommand(string.Format("{0}?", PowerOpCode));
        }

        public void InputSelect(ushort input)
        {
            if (_PowerIsOn)
            {
                _RequestedInputState = input;
                InputGo();
            }
            else if (_RequestedPowerState == 1)
            {
                _RequestedInputState = input;
            }
        }

        public void InputGo()
        {
            if (_CurrentInputIndex == _RequestedInputState) return;
            SendCommand(string.Format("{0}({1})", PresetRecallOpCode, _RequestedInputState));
            InputGet();
        }

        public void InputGet()
        {
            SendCommand(string.Format("{0}?", PresetActiveOpCode));
        }


        private class PlanarQueue
        {
            private readonly List<string> Q = new List<string>();

            public ushort Count
            {
                get { return (ushort)Q.Count; }
            }

            private readonly CMutex mutex = new CMutex();

            /// <summary>
            /// Creates a queue for processing commands
            /// </summary>
            public PlanarQueue()
            {
            }

            public void AddOrUpdateCommand(string command)
            {
                mutex.WaitForMutex();
                try
                {
                    int i = Q.FindIndex(x => x.Equals(command));
                    if (i == -1)
                    {
                        Q.Add(command);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in planar command queue add/update: {0}", ex);
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
                    Debug.Console(1, "Exception in planar command queue clear: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            public string Dequeue()
            {
                mutex.WaitForMutex();
                try
                {
                    if (Q.Count > 0)
                    {
                        string cmd = Q[0];
                        Q.RemoveAt(0);
                        return cmd;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Console(1, "Exception in planar command queue dequeue: {0}", ex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                return null;
            }
        }
    }

    public class PlanarRpsFactory : EssentialsDeviceFactory<PlanarRps>
    {
        public PlanarRpsFactory()
        {
            TypeNames = new List<string>() { "planarrps" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Planar Rps Device");

            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);
            if (comm != null)
                return new PlanarRps(dc.Key, dc.Name, comm);
            return null;
        }
    }
}