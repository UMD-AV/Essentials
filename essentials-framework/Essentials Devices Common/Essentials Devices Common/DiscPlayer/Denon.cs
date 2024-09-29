using System;
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
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.Devices.Common.Denon
{
    public class DenonBdpDevice : EssentialsBridgeableDevice
    {
        #region constants
        private const string CmdPwrOn = "PW00";
        private const string CmdPwrOff = "PW01";
        private const string CmdSetup = "PCSU";
        private const string CmdTopMenu = "DVTP";
        private const string CmdMenu = "DVOP";
        private const string CmdReturn = "PCRTN";
        private const string CmdPlay = "2353";
        private const string CmdStop = "2354";
        private const string CmdPause = "2348";
        private const string CmdNext = "2332";
        private const string CmdPrev = "2333";
        private const string CmdFForward = "PCGPPV";
        private const string CmdRewind = "PCSLSR";
        private const string CmdRed = "DVFCLR1";
        private const string CmdGreen = "DVFCLR2";
        private const string CmdYellow = "DVFCLR4";
        private const string CmdBlue = "DVFCLR3";
        private const string QueryPower = "?PW";
        private const string QueryStatus = "?ST";
        private const string QueryVersion = "?VN";
        private const string ParamDigit0 = "PCTKEY0";
        private const string ParamDigit1 = "PCTKEY1";
        private const string ParamDigit2 = "PCTKEY2";
        private const string ParamDigit3 = "PCTKEY3";
        private const string ParamDigit4 = "PCTKEY4";
        private const string ParamDigit5 = "PCTKEY5";
        private const string ParamDigit6 = "PCTKEY6";
        private const string ParamDigit7 = "PCTKEY7";
        private const string ParamDigit8 = "PCTKEY8";
        private const string ParamDigit9 = "PCTKEY9";
        private const string ParamUp = "PCCUSR3";
        private const string ParamDown = "PCCUSR4";
        private const string ParamLeft = "PCCUSR1";
        private const string ParamRight = "PCCUSR2";
        private const string ParamSelect = "PCENTR";
        #endregion

        private uint pollCount;
        readonly DenonQueue _cmdQueue;
        CMutex _CommandMutex;
        DeviceConfig _Dc;
        private bool _readyForNextCommand;
        private string _lastCommand;

        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        public DenonBdpDevice(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            _Dc = dc;
            PowerIsOnFeedback = new BoolFeedback(() => _PowerIsOn);
            pollCount = 0;
            _cmdQueue = new DenonQueue();
            _CommandMutex = new CMutex();
            _readyForNextCommand = true;
            _lastCommand = "";

            Communication = comm;
            Communication.BytesReceived += Communication_BytesReceived;

            var socket = comm as ISocketStatus;
            if (socket != null)
            {
                // This instance uses IP control
                socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
            }
            else
            {
                // This instance uses RS-232 control
            }

            // Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 20s if subscriptions are valid
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 20000, 120000, 300000, Poll);
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
            // Essentials will handle the connect method to the device
            Communication.Connect();
            // Essentials will handle starting the comms monitor
            CommunicationMonitor.Start();

            return base.CustomActivate();
            //return true;
        }

        private void QueueCommand(string command)
        {

            _cmdQueue.AddCommand(command);
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            bool test = _CommandMutex.WaitForMutex(100);
            if (test)
            {
                //Pace the commands sending out
                while (_cmdQueue.Count > 0)
                {
                    try
                    {
                        string command;
                        command = _cmdQueue.Dequeue();
                        if (command != null)
                        {
                            var fullCommand = string.Format("@0{0}\x0D", command);
                            _readyForNextCommand = false;

                            int count = 0;
                            while (!_readyForNextCommand && count < 3)
                            {

                                Debug.Console(1, this, "Sending Text: {0}", fullCommand);
                                _lastCommand = command;
                                Communication.SendText(fullCommand);
                                if (command == CmdPwrOn)
                                {
                                    Thread.Sleep(1000);
                                }
                                else
                                {
                                    Thread.Sleep(300);
                                }
                                count++;
                            }
                            if (count >= 3)
                            {
                                Debug.Console(0, this, "Failed to send {0}", fullCommand);
                                _readyForNextCommand = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Console(0, this, "Caught an exception in ProcessQueue {0}\r{1}\r{2}", ex.Message, ex.InnerException, ex.StackTrace);
                    }
                }
                _CommandMutex.ReleaseMutex();
            }
        }

        void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            Debug.Console(2, this, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());

            if (e.Client.IsConnected)
            {

            }
            else if (!e.Client.IsConnected)
            {

            }
        }

        /// <summary>
        /// Communication bytes recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            if (e.Bytes.Length > 2 && e.Bytes[0] == 0x61 && e.Bytes[1] == 0x63 && e.Bytes[2] == 0x6B)
            {
                Debug.Console(2, this, "Received ACK");
                if (_lastCommand == CmdPwrOn)
                {
                    _PowerIsOn = true;
                    PowerIsOnFeedback.FireUpdate();
                }
                else if (_lastCommand == CmdPwrOff)
                {
                    _PowerIsOn = false;
                    PowerIsOnFeedback.FireUpdate();
                }
                else if (!_PowerIsOn)
                {
                    _PowerIsOn = true;
                    PowerIsOnFeedback.FireUpdate();
                }
                _readyForNextCommand = true;
            }
            else if (e.Bytes[0] == 0x40)
            {
                if (_readyForNextCommand)
                {
                    if (!_PowerIsOn)
                    {
                        _PowerIsOn = true;
                        PowerIsOnFeedback.FireUpdate();
                    }
                    Debug.Console(2, this, "Received feedback, sending confirm receipt");
                    Communication.SendText("ACK");
                }
            }
            else if (e.Bytes.Length > 5 && e.Bytes[2] == 0x6E && e.Bytes[3] == 0x61 && e.Bytes[4] == 0x63 && e.Bytes[5] == 0x6B)
            {
                Debug.Console(2, this, "Received NACK");
                if (_PowerIsOn)
                {
                    _PowerIsOn = false;
                    PowerIsOnFeedback.FireUpdate();
                }
                _readyForNextCommand = true;
            }
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            {
                var joinMap = new DenonBdpJoinMap(joinStart);
                // This adds the join map to the collection on the bridge
                if (bridge != null)
                {
                    bridge.AddJoinMap(Key, joinMap);
                }

                var joinMapSerialized = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
                if (joinMapSerialized != null)
                {
                    joinMap.SetCustomJoinData(joinMapSerialized);
                }

                CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

                PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
                PowerIsOnFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);
                PowerIsOnFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerToggle.JoinNumber]);

                trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

                trilist.SetSigTrueAction(joinMap.PowerOn.JoinNumber, PowerOn);
                trilist.SetSigTrueAction(joinMap.PowerOff.JoinNumber, PowerOff);
                trilist.SetSigTrueAction(joinMap.PowerToggle.JoinNumber, PowerToggle);

                trilist.SetSigTrueAction(joinMap.Up.JoinNumber, Up);
                trilist.SetSigTrueAction(joinMap.Down.JoinNumber, Down);
                trilist.SetSigTrueAction(joinMap.Left.JoinNumber, Left);
                trilist.SetSigTrueAction(joinMap.Right.JoinNumber, Right);
                trilist.SetSigTrueAction(joinMap.Select.JoinNumber, Select);
                trilist.SetSigTrueAction(joinMap.Menu.JoinNumber, Menu);
                trilist.SetSigTrueAction(joinMap.Exit.JoinNumber, Return);
                trilist.SetSigTrueAction(joinMap.TopMenu.JoinNumber, TopMenu);
                trilist.SetSigTrueAction(joinMap.Setup.JoinNumber, Setup);

                trilist.SetSigTrueAction(joinMap.Play.JoinNumber, Play);
                trilist.SetSigTrueAction(joinMap.Stop.JoinNumber, Stop);
                trilist.SetSigTrueAction(joinMap.Pause.JoinNumber, Pause);
                trilist.SetSigTrueAction(joinMap.ChapPlus.JoinNumber, ChapPlus);
                trilist.SetSigTrueAction(joinMap.ChapMinus.JoinNumber, ChapMinus);
                trilist.SetSigTrueAction(joinMap.FFwd.JoinNumber, FFwd);
                trilist.SetSigTrueAction(joinMap.Rewind.JoinNumber, Rewind);
                trilist.SetSigTrueAction(joinMap.Red.JoinNumber, Red);
                trilist.SetSigTrueAction(joinMap.Green.JoinNumber, Green);
                trilist.SetSigTrueAction(joinMap.Yellow.JoinNumber, Yellow);
                trilist.SetSigTrueAction(joinMap.Blue.JoinNumber, Blue);

                trilist.SetSigTrueAction(joinMap.Digit0.JoinNumber, Digit0);
                trilist.SetSigTrueAction(joinMap.Digit1.JoinNumber, Digit1);
                trilist.SetSigTrueAction(joinMap.Digit2.JoinNumber, Digit2);
                trilist.SetSigTrueAction(joinMap.Digit3.JoinNumber, Digit3);
                trilist.SetSigTrueAction(joinMap.Digit4.JoinNumber, Digit4);
                trilist.SetSigTrueAction(joinMap.Digit5.JoinNumber, Digit5);
                trilist.SetSigTrueAction(joinMap.Digit6.JoinNumber, Digit6);
                trilist.SetSigTrueAction(joinMap.Digit7.JoinNumber, Digit7);
                trilist.SetSigTrueAction(joinMap.Digit8.JoinNumber, Digit8);
                trilist.SetSigTrueAction(joinMap.Digit9.JoinNumber, Digit9);
            }
        }

        #endregion

        public void Setup()
        {
            QueueCommand(CmdSetup);
        }

        public void TopMenu()
        {
            QueueCommand(CmdTopMenu);
        }

        #region IDPad Members

        public void Up()
        {
            QueueCommand(ParamUp);
        }

        public void Down()
        {
            QueueCommand(ParamDown);
        }

        public void Left()
        {
            QueueCommand(ParamLeft);
        }

        public void Right()
        {
            QueueCommand(ParamRight);            
        }

        public void Select()
        {
            QueueCommand(ParamSelect);            
        }

        public void Menu()
        {
            QueueCommand(CmdMenu);            
        }

        public void Return()
        {
            QueueCommand(CmdReturn);            
        }

        #endregion

        #region INumericKeypad Members

        public void Digit0()
        {
            QueueCommand(ParamDigit0);            
        }

        public void Digit1()
        {
            QueueCommand(ParamDigit1);            
        }

        public void Digit2()
        {
            QueueCommand(ParamDigit2);            
        }

        public void Digit3()
        {
            QueueCommand(ParamDigit3);            
        }

        public void Digit4()
        {
            QueueCommand(ParamDigit4);            
        }

        public void Digit5()
        {
            QueueCommand(ParamDigit5);            
        }

        public void Digit6()
        {
            QueueCommand(ParamDigit6);            
        }

        public void Digit7()
        {
            QueueCommand(ParamDigit7);            
        }

        public void Digit8()
        {
            QueueCommand(ParamDigit8);            
        }

        public void Digit9()
        {
            QueueCommand(ParamDigit9);            
        }
        #endregion

        #region IColorFunctions Members

        public void Red()
        {
            QueueCommand(CmdRed);            
        }

        public void Green()
        {
            QueueCommand(CmdGreen);            
        }

        public void Yellow()
        {
            QueueCommand(CmdYellow);            
        }

        public void Blue()
        {
            QueueCommand(CmdBlue);            
        }

        #endregion

        #region IPower Members

        public void PowerOn()
        {
            QueueCommand(CmdPwrOn);            
        }

        public void PowerOff()
        {
            QueueCommand(CmdPwrOff);            
        }

        public void PowerToggle()
        {
            if (_PowerIsOn)
            {
                PowerOn();
            }
            else
            {
                PowerOff();
            }
        }

        public BoolFeedback PowerIsOnFeedback { get; set; }
        bool _PowerIsOn;

        #endregion

        #region ITransport Members

        public void Play()
        {
            QueueCommand(CmdPlay);            
        }

        public void Pause()
        {
            QueueCommand(CmdPause);            
        }

        public void Rewind()
        {
            QueueCommand(CmdRewind);            
        }

        public void FFwd()
        {
            QueueCommand(CmdFForward);            
        }

        public void ChapMinus()
        {
            QueueCommand(CmdPrev);            
        }

        public void ChapPlus()
        {
            QueueCommand(CmdNext);            
        }

        public void Stop()
        {
            QueueCommand(CmdStop);            
        }

        #endregion

        #region Poll
        public void Poll()
        {
            switch (pollCount)
            {
                case 0:
                    QueueCommand(QueryPower);                    
                    break;
                case 1:
                    QueueCommand(QueryStatus);                    
                    break;
                case 2:
                    QueueCommand(QueryVersion);                    
                    break;
            }
            pollCount++;
            if (pollCount > 2)
            {
                pollCount = 0;
            }
        }

        #endregion

    }

    public class DenonQueue
    {
        public List<string> Q = new List<string>();
        public ushort Count { get { return (ushort)Q.Count; } }
        private CMutex mutex = new CMutex();

        /// <summary>
        /// Creates a queue for processing Denon bluray commands
        /// </summary>
        public DenonQueue()
        {
        }

        public void AddCommand(string command)
        {
            mutex.WaitForMutex();
            try
            {
                Q.Add(command);
            }
            catch (Exception ex)
            {
                Debug.Console(1, "Exception in Denon command queue add: {0}", ex);
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
                Debug.Console(1, "Exception in Denon command queue clear: {0}", ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public string Dequeue()
        {
            string cmd = null;
            mutex.WaitForMutex();
            try
            {
                if (Q.Count > 0)
                {
                    cmd = Q[0];
                    Q.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, "Exception in Denon command queue dequeue: {0}", ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            return cmd;
        }
    }

    public class DenonBdpJoinMap : IRBlurayBaseJoinMap
    {
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 49,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "IsOnline",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Setup")]
        public JoinDataComplete Setup = new JoinDataComplete(new JoinData { JoinNumber = 44, JoinSpan = 1 },
            new JoinMetadata { Description = "Setup", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("TopMenu")]
        public JoinDataComplete TopMenu = new JoinDataComplete(new JoinData { JoinNumber = 45, JoinSpan = 1 },
            new JoinMetadata { Description = "Top Menu", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        public DenonBdpJoinMap(uint joinStart)
            : base(joinStart, typeof(DenonBdpJoinMap))
        {

        }
    }

    public class DenonBdpFactory : EssentialsDeviceFactory<DenonBdpDevice>
    {
        public DenonBdpFactory()
        {
            TypeNames = new List<string> { "denonbdp" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Denon BDP device");

            var comms = CommFactory.CreateCommForDevice(dc);

            return new DenonBdpDevice(dc.Key, dc.Name, comms, dc);
        }
    }
}
