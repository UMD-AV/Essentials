using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;

namespace PepperDash.Essentials.Devices.Common.Oppo
{
    public class OppoBdpDevice : EssentialsBridgeableDevice
    {
        #region constants

        private const string VerboseMode2 = "SVM 2";
        private const string CmdPwrOn = "PON";
        private const string CmdPwrOff = "POF";
        private const string CmdPwrToggle = "POW";
        private const string CmdSetup = "SET";
        private const string CmdTopMenu = "TTL";
        private const string CmdMenu = "MNU";
        private const string CmdReturn = "RET";
        private const string CmdPlay = "PLA";
        private const string CmdStop = "STP";
        private const string CmdPause = "PAU";
        private const string CmdNext = "NXT";
        private const string CmdPrev = "PRE";
        private const string CmdFForward = "FWD";
        private const string CmdRewind = "REV";
        private const string CmdRed = "RED";
        private const string CmdGreen = "GRN";
        private const string CmdYellow = "YLW";
        private const string CmdBlue = "BLU";
        private const string QueryVerbose = "QVM";
        private const string QueryPower = "QPW";
        private const string QueryStatus = "QPL";
        private const string QueryVolume = "QVL";
        private const string ParamDigit0 = "NU0";
        private const string ParamDigit1 = "NU1";
        private const string ParamDigit2 = "NU2";
        private const string ParamDigit3 = "NU3";
        private const string ParamDigit4 = "NU4";
        private const string ParamDigit5 = "NU5";
        private const string ParamDigit6 = "NU6";
        private const string ParamDigit7 = "NU7";
        private const string ParamDigit8 = "NU8";
        private const string ParamDigit9 = "NU9";
        private const string ParamUp = "NUP";
        private const string ParamDown = "NDN";
        private const string ParamLeft = "NLT";
        private const string ParamRight = "NRT";
        private const string ParamSelect = "SEL";

        #endregion

        private uint pollCount;
        private DeviceConfig _Dc;

        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        public OppoBdpDevice(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            _Dc = dc;
            PowerIsOnFeedback = new BoolFeedback(() => _PowerIsOn);
            pollCount = 0;

            Communication = comm;
            ISocketStatus socket = comm as ISocketStatus;
            if (socket != null)
            {
                // This instance uses IP control
                socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
            }
            else
            {
                // This instance uses RS-232 control
            }

            PortGather = new CommunicationGather(Communication, "\x0D");
            PortGather.LineReceived += this.Port_LineReceived;

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

        private string BuildCommand(string command)
        {
            string cmd = string.Format("#{0}\x0D", command);
            return cmd;
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            Debug.Console(2, this, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());

            if (e.Client.IsConnected)
            {
            }
            else if (!e.Client.IsConnected)
            {
            }
        }

        private void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "RX : '{0}'", args.Text);
            string message = args.Text.Trim();
            if (message.StartsWith("@OK"))
            {
                if (message.Equals("@OK 0") || message.Equals("@OK 1") || message.Equals("@OK 3"))
                {
                    Communication.SendText(BuildCommand(VerboseMode2));
                }
            }
            else if (message.StartsWith("@UPW"))
            {
                if (message.Equals("@UPW 0"))
                {
                    _PowerIsOn = false;
                    PowerIsOnFeedback.FireUpdate();
                }
                else if (message.Equals("@UPW 1"))
                {
                    _PowerIsOn = true;
                    PowerIsOnFeedback.FireUpdate();
                }
            }
            else if (message.Equals("APW 1"))
            {
                _PowerIsOn = true;
                PowerIsOnFeedback.FireUpdate();
            }
            else if (message.StartsWith("@QPW"))
            {
                if (message.Equals("@QPW OK OFF"))
                {
                    _PowerIsOn = false;
                    PowerIsOnFeedback.FireUpdate();
                }
                else if (message.Equals("@QPW OK ON"))
                {
                    _PowerIsOn = true;
                    PowerIsOnFeedback.FireUpdate();
                }
            }
        }


        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            {
                OppoBdpJoinMap joinMap = new OppoBdpJoinMap(joinStart);
                // This adds the join map to the collection on the bridge
                if (bridge != null)
                {
                    bridge.AddJoinMap(Key, joinMap);
                }

                Dictionary<string, JoinData> joinMapSerialized =
                    JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);
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
            Communication.SendText(BuildCommand(CmdSetup));
        }

        public void TopMenu()
        {
            Communication.SendText(BuildCommand(CmdTopMenu));
        }

        #region IDPad Members

        public void Up()
        {
            Communication.SendText(BuildCommand(ParamUp));
        }

        public void Down()
        {
            Communication.SendText(BuildCommand(ParamDown));
        }

        public void Left()
        {
            Communication.SendText(BuildCommand(ParamLeft));
        }

        public void Right()
        {
            Communication.SendText(BuildCommand(ParamRight));
        }

        public void Select()
        {
            Communication.SendText(BuildCommand(ParamSelect));
        }

        public void Menu()
        {
            Communication.SendText(BuildCommand(CmdMenu));
        }

        public void Return()
        {
            Communication.SendText(BuildCommand(CmdReturn));
        }

        #endregion

        #region INumericKeypad Members

        public void Digit0()
        {
            Communication.SendText(BuildCommand(ParamDigit0));
        }

        public void Digit1()
        {
            Communication.SendText(BuildCommand(ParamDigit1));
        }

        public void Digit2()
        {
            Communication.SendText(BuildCommand(ParamDigit2));
        }

        public void Digit3()
        {
            Communication.SendText(BuildCommand(ParamDigit3));
        }

        public void Digit4()
        {
            Communication.SendText(BuildCommand(ParamDigit4));
        }

        public void Digit5()
        {
            Communication.SendText(BuildCommand(ParamDigit5));
        }

        public void Digit6()
        {
            Communication.SendText(BuildCommand(ParamDigit6));
        }

        public void Digit7()
        {
            Communication.SendText(BuildCommand(ParamDigit7));
        }

        public void Digit8()
        {
            Communication.SendText(BuildCommand(ParamDigit8));
        }

        public void Digit9()
        {
            Communication.SendText(BuildCommand(ParamDigit9));
        }

        #endregion

        #region IColorFunctions Members

        public void Red()
        {
            Communication.SendText(BuildCommand(CmdRed));
        }

        public void Green()
        {
            Communication.SendText(BuildCommand(CmdGreen));
        }

        public void Yellow()
        {
            Communication.SendText(BuildCommand(CmdYellow));
        }

        public void Blue()
        {
            Communication.SendText(BuildCommand(CmdBlue));
        }

        #endregion

        #region IPower Members

        public void PowerOn()
        {
            Communication.SendText(BuildCommand(CmdPwrOn));
        }

        public void PowerOff()
        {
            Communication.SendText(BuildCommand(CmdPwrOff));
        }

        public void PowerToggle()
        {
            Communication.SendText(BuildCommand(CmdPwrToggle));
        }

        public BoolFeedback PowerIsOnFeedback { get; set; }
        private bool _PowerIsOn;

        #endregion

        #region ITransport Members

        public void Play()
        {
            Communication.SendText(BuildCommand(CmdPlay));
        }

        public void Pause()
        {
            Communication.SendText(BuildCommand(CmdPause));
        }

        public void Rewind()
        {
            Communication.SendText(BuildCommand(CmdRewind));
        }

        public void FFwd()
        {
            Communication.SendText(BuildCommand(CmdFForward));
        }

        public void ChapMinus()
        {
            Communication.SendText(BuildCommand(CmdPrev));
        }

        public void ChapPlus()
        {
            Communication.SendText(BuildCommand(CmdNext));
        }

        public void Stop()
        {
            Communication.SendText(BuildCommand(CmdStop));
        }

        #endregion

        #region Poll

        public void Poll()
        {
            switch (pollCount)
            {
                case 0:
                    Communication.SendText(BuildCommand(QueryVerbose));
                    break;
                case 1:
                    Communication.SendText(BuildCommand(QueryPower));
                    break;
            }

            pollCount++;
            if (pollCount > 1)
            {
                pollCount = 0;
            }
        }

        #endregion
    }

    public class OppoBdpJoinMap : IRBlurayBaseJoinMap
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
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

        [JoinName("Setup")] public JoinDataComplete Setup = new JoinDataComplete(
            new JoinData { JoinNumber = 44, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Setup", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital
            });

        [JoinName("TopMenu")] public JoinDataComplete TopMenu = new JoinDataComplete(
            new JoinData { JoinNumber = 45, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Top Menu", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital
            });

        public OppoBdpJoinMap(uint joinStart)
            : base(joinStart, typeof(OppoBdpJoinMap))
        {
        }
    }

    public class OppoBdpFactory : EssentialsDeviceFactory<OppoBdpDevice>
    {
        public OppoBdpFactory()
        {
            TypeNames = new List<string> { "oppobdp" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Oppo BDP device");

            IBasicCommunication comms = CommFactory.CreateCommForDevice(dc);

            return new OppoBdpDevice(dc.Key, dc.Name, comms, dc);
        }
    }
}