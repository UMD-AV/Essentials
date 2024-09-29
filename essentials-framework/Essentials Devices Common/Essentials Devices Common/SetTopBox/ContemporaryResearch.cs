using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Newtonsoft.Json;
using PepperDash.Essentials.Core.Config;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.ContemporaryResearch
{
    public class ContemporaryResearchDevice : EssentialsBridgeableDevice, ISetTopBoxControls
    {
        #region constants
        private const string Attention = ">";

        private const string CmdPwrOn = "P1";

        private const string CmdPwrOff = "P0";

        private const string CmdChanUp = "TU";

        private const string CmdChanDn = "TD";

        private const string CmdChanLast = "TP";

        private const string CmdKeyEmu = "KK";

        private const string CmdPoll = "ST";

        private const string ParamDigit0 = "=10";
        private const string ParamDigit1 = "=11";
        private const string ParamDigit2 = "=12";
        private const string ParamDigit3 = "=13";
        private const string ParamDigit4 = "=14";
        private const string ParamDigit5 = "=15";
        private const string ParamDigit6 = "=16";
        private const string ParamDigit7 = "=17";
        private const string ParamDigit8 = "=18";
        private const string ParamDigit9 = "=19";
        private const string ParamDash = "=99";
        private const string ParamKpEnter = "=21";
        private const string ParamMenu = "=29";
        private const string ParamGuide = "=63";
        private const string ParamInfo = "=100";
        private const string ParamUp = "=108";
        private const string ParamDn = "=109";
        private const string ParamLt = "=107";
        private const string ParamRt = "=106";
        private const string ParamDpEnter = "=110";
        private const string ParamExit = "=111";


        #endregion

        DeviceConfig _Dc;

        private Properties _props;

        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        private string UnitId { get; set; }

        BoolFeedback PowerStatusFeedback { get; set; }
        private bool _PowerStatus { get; set; }
        private bool PowerStatus
        {
            get
            {
                return _PowerStatus;
            }
            set
            {
                _PowerStatus = value;
                PowerStatusFeedback.FireUpdate();
            }
        }

        public ContemporaryResearchDevice(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            _Dc = dc;

            _props = JsonConvert.DeserializeObject<Properties>(dc.Properties.ToString());

            UnitId = _props.unitId;

            PowerStatusFeedback = new BoolFeedback(() => PowerStatus);

            Communication = comm;
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

            PortGather = new CommunicationGather(Communication, "\x0A");
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

        private string BuildCommand(string command, string parameter)
        {
            var cmd = string.Format("{0}{1}{2}{3}\x0D", Attention, UnitId, command, parameter);
            //Debug.Console(2, this, "TX : '{0}' ", cmd);
            return cmd;
        }

        private string BuildCommand(string command)
        {
            var cmd = string.Format("{0}{1}{2}\x0D", Attention, UnitId, command);
            //Debug.Console(2, this, "TX : '{0}'", cmd);
            return cmd;
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

        void Port_LineReceived(object dev, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "RX : '{0}'", args.Text);
            var header = string.Format("<{0}T", UnitId);
            if (args.Text.Contains(header))
            {
                var message = args.Text.Substring(3, 1);
                if (message.Equals("U"))
                {
                    PowerStatus = true;
                }
                else if (message.Equals("M"))
                {
                    PowerStatus = false;
                }
            }
        }


        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            {
                var joinMap = new ContemporaryResearchJoinMap(joinStart);
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
                PowerStatusFeedback.LinkInputSig(trilist.BooleanInput[joinMap.PowerOn.JoinNumber]);
                PowerStatusFeedback.LinkComplementInputSig(trilist.BooleanInput[joinMap.PowerOff.JoinNumber]);

                trilist.StringInput[joinMap.Name.JoinNumber].StringValue = Name;

                trilist.SetBoolSigAction(joinMap.PowerOn.JoinNumber, PowerOn);
                trilist.SetBoolSigAction(joinMap.PowerOff.JoinNumber, PowerOff);
                trilist.SetBoolSigAction(joinMap.PowerToggle.JoinNumber, PowerToggle);

                trilist.SetBoolSigAction(joinMap.Up.JoinNumber, Up);
                trilist.SetBoolSigAction(joinMap.Down.JoinNumber, Down);
                trilist.SetBoolSigAction(joinMap.Left.JoinNumber, Left);
                trilist.SetBoolSigAction(joinMap.Right.JoinNumber, Right);
                trilist.SetBoolSigAction(joinMap.Select.JoinNumber, Select);
                trilist.SetBoolSigAction(joinMap.Menu.JoinNumber, Menu);
                trilist.SetBoolSigAction(joinMap.Exit.JoinNumber, Exit);

                trilist.SetBoolSigAction(joinMap.ChannelUp.JoinNumber, ChannelUp);
                trilist.SetBoolSigAction(joinMap.ChannelDown.JoinNumber, ChannelDown);
                trilist.SetBoolSigAction(joinMap.LastChannel.JoinNumber, LastChannel);
                trilist.SetBoolSigAction(joinMap.Guide.JoinNumber, Guide);
                trilist.SetBoolSigAction(joinMap.Info.JoinNumber, Info);
                trilist.SetBoolSigAction(joinMap.Exit.JoinNumber, Exit);

                trilist.SetBoolSigAction(joinMap.Digit0.JoinNumber, Digit0);
                trilist.SetBoolSigAction(joinMap.Digit1.JoinNumber, Digit1);
                trilist.SetBoolSigAction(joinMap.Digit2.JoinNumber, Digit2);
                trilist.SetBoolSigAction(joinMap.Digit3.JoinNumber, Digit3);
                trilist.SetBoolSigAction(joinMap.Digit4.JoinNumber, Digit4);
                trilist.SetBoolSigAction(joinMap.Digit5.JoinNumber, Digit5);
                trilist.SetBoolSigAction(joinMap.Digit6.JoinNumber, Digit6);
                trilist.SetBoolSigAction(joinMap.Digit7.JoinNumber, Digit7);
                trilist.SetBoolSigAction(joinMap.Digit8.JoinNumber, Digit8);
                trilist.SetBoolSigAction(joinMap.Digit9.JoinNumber, Digit9);
                trilist.SetBoolSigAction(joinMap.Dash.JoinNumber, Dash);
                trilist.SetBoolSigAction(joinMap.KeypadEnter.JoinNumber, KeypadEnter);
            }
        }

        #endregion

        #region ISetTopBoxControls Members

        public void DvrList(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public bool HasDpad
        {
            get { throw new NotImplementedException(); }
        }

        public bool HasDvr
        {
            get { throw new NotImplementedException(); }
        }

        public bool HasNumeric
        {
            get { throw new NotImplementedException(); }
        }

        public bool HasPresets
        {
            get { throw new NotImplementedException(); }
        }

        public void LoadPresets(string filePath)
        {
            throw new NotImplementedException();
        }

        public PepperDash.Essentials.Core.Presets.DevicePresetsModel PresetsModel
        {
            get { throw new NotImplementedException(); }
        }

        public void Replay(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IChannel Members

        public void ChannelDown(bool pressRelease)
        {
            if (pressRelease)
                Communication.SendText(BuildCommand(CmdChanDn));
        }

        public void ChannelUp(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdChanUp));
        }

        public void Exit(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamExit));
        }

        public void Guide(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamGuide));
        }

        public void Info(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamInfo));
        }

        public void LastChannel(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdChanLast));
        }

        #endregion

        public void PowerOn(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdPwrOn));
        }

        public void PowerOff(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdPwrOff));
        }

        public void PowerToggle(bool pressRelease)
        {
            if (pressRelease)
            {
                if (PowerStatus)
                {
                    Communication.SendText(BuildCommand(CmdPwrOff));
                }
                else
                {
                    Communication.SendText(BuildCommand(CmdPwrOn));
                }
            }

        }

        #region IColor Members

        public void Blue(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Green(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Red(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Yellow(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDPad Members

        public void Down(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDn));
        }

        public void Left(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamLt));
        }

        public void Menu(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamMenu));
        }

        public void Right(bool pressRelease)
        {
            Communication.SendText(BuildCommand(CmdKeyEmu, ParamRt));
        }

        public void Select(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDpEnter));
        }

        public void Up(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamUp));
        }

        #endregion

        #region ISetTopBoxNumericKeypad Members

        public void Dash(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDash));
        }

        public void KeypadEnter(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamKpEnter));
        }

        #endregion

        #region INumericKeypad Members

        public void Digit0(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit0));
        }

        public void Digit1(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit1));
        }

        public void Digit2(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit2));
        }

        public void Digit3(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit3));
        }

        public void Digit4(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit4));
        }

        public void Digit5(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit5));
        }

        public void Digit6(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit6));
        }

        public void Digit7(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit7));
        }

        public void Digit8(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit8));
        }

        public void Digit9(bool pressRelease)
        {
            if (pressRelease)

                Communication.SendText(BuildCommand(CmdKeyEmu, ParamDigit9));
        }

        public bool HasKeypadAccessoryButton1
        {
            get { throw new NotImplementedException(); }
        }

        public bool HasKeypadAccessoryButton2
        {
            get { throw new NotImplementedException(); }
        }

        public void KeypadAccessoryButton1(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public string KeypadAccessoryButton1Label
        {
            get { throw new NotImplementedException(); }
        }

        public void KeypadAccessoryButton2(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public string KeypadAccessoryButton2Label
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region ITransport Members

        public void ChapMinus(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void ChapPlus(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void FFwd(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Pause(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Play(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Record(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Rewind(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        public void Stop(bool pressRelease)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Poll
        public void Poll()
        {
            Communication.SendText(BuildCommand(CmdPoll));
        }

        #endregion

        #region IUiDisplayInfo Members

        public uint DisplayUiType
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region ISetTopBoxControls Members


        public PepperDash.Essentials.Core.Presets.DevicePresetsModel TvPresets
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }

    public class Properties
    {
        public EssentialsControlPropertiesConfig Control { get; set; }

        public string unitId { get; set; }
    }

    public class ContemporaryResearchJoinMap : SetTopBoxControllerJoinMap
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
        public ContemporaryResearchJoinMap(uint joinStart)
            : base(joinStart, typeof(ContemporaryResearchJoinMap))
        {

        }
    }

    public class ContemporaryResearchFactory : EssentialsDeviceFactory<ContemporaryResearchDevice>
    {
        public ContemporaryResearchFactory()
        {
            TypeNames = new List<string> { "contemporaryresearch" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Contemporary Research device");

            var comms = CommFactory.CreateCommForDevice(dc);

            return new ContemporaryResearchDevice(dc.Key, dc.Name, comms, dc);
        }
    }
}
