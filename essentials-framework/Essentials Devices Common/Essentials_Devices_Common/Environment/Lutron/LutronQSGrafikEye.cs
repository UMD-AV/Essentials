using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Lighting;
using LightingBase = PepperDash.Essentials.Core.Lighting.LightingBase;

namespace PepperDash.Essentials.Devices.Common.Environment.Lutron
{
    public class LutronQS : LightingBase, ICommunicationMonitor
    {
        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        private LutronQuantumPropertiesConfig _props;

        private string _integrationId;

        public string IntegrationId
        {
            get { return _integrationId; }
            set
            {
                if (value.Length > 0)
                {
                    if (_integrationId == value) return;
                    _integrationId = value;
                    UpdateConfigIntegrationId(value);
                }
            }
        }

        private string Username;
        private string Password;

        private const string SceneController = "141";
        private const string Delimiter = "\x0d";
        private const string Set = "#";
        private const string Get = "?";

        public LutronQS(string key, string name, IBasicCommunication comm, LutronQuantumPropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            _props = props;
            IntegrationId = props.IntegrationId;

            if (props.Control.Method != eControlMethod.Com)
            {
                Username = props.Control.TcpSshProperties.Username;
                Password = props.Control.TcpSshProperties.Password;
            }

            LightingScenes = props.Scenes;

            ISocketStatus socket = comm as ISocketStatus;
            if (socket != null)
            {
                // IP Control
                socket.ConnectionChange += new EventHandler<GenericSocketStatusChageEventArgs>(socket_ConnectionChange);
            }

            Communication.TextReceived +=
                new EventHandler<GenericCommMethodReceiveTextArgs>(Communication_TextReceived);

            PortGather = new CommunicationGather(Communication, Delimiter);
            PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(PortGather_LineReceived);

            if (props.CommunicationMonitorProperties != null)
            {
                CommunicationMonitor =
                    new GenericCommunicationMonitor(this, Communication, props.CommunicationMonitorProperties);
            }
            else
            {
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 50000, 120000, 300000,
                    "?ETHERNET,0\x0d\x0a");
            }
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.StatusChange += (o, a) =>
            {
                Debug.Console(2, this, "Communication monitor state: {0}", CommunicationMonitor.Status);
            };
            CommunicationMonitor.Start();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericLightingJoinMap joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.SetStringSigAction(joinMap.IntegrationIdSet.JoinNumber, s => IntegrationId = s);
            trilist.SetSigTrueAction(joinMap.QueryDevices.JoinNumber, QueryAllDevices);
            trilist.SetSigTrueAction(joinMap.QueryIDs.JoinNumber, QueryAllIntegrationIds);
        }

        private void UpdateConfigIntegrationId(string id)
        {
            if (_props.IntegrationId != id)
            {
                _props.IntegrationId = id;
                //ConfigWriter.UpdateDeviceProperties(this.Key, JToken.FromObject(_props));
            }
        }

        private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs e)
        {
            Debug.Console(2, this, "Socket Status Change: {0}", e.Client.ClientStatus.ToString());
        }

        /// <summary>
        /// Checks for responses that do not contain the delimiter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Communication_TextReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "Text Received: '{0}'", args.Text);

            if (args.Text.Contains("login:"))
            {
                // Login
                SendLine(Username);
            }
            else if (args.Text.Contains("password:"))
            {
                // Login
                SendLine(Password);
            }
        }

        /// <summary>
        /// Handles all responses that contain the delimiter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "Line Received: '{0}'", args.Text);

            try
            {
                if (args.Text.Contains("~DEVICE"))
                {
                    string[] response = args.Text.Split(',');

                    string integrationId = response[1];

                    if (integrationId != IntegrationId)
                    {
                        Debug.Console(2, this, "Response is not for correct Integration ID");
                        return;
                    }
                    else
                    {
                        //Found scene controller on grafikeye
                        if (response[2] == SceneController && response.Length >= 5)
                        {
                            if (response[3] == "7")
                            {
                                Debug.Console(2, this, "Found lighting scene {0}", response[4]);
                                LightingScene match = LightingScenes.FirstOrDefault(s => s.ID.Equals(response[4]));
                                if (match != null)
                                {
                                    CurrentLightingScene = match;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Console(2, this, "Error parsing response:\n{0}", e);
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public override void SelectScene(LightingScene scene)
        {
            Debug.Console(1, this, "Selecting Scene: '{0}'", scene.Name);
            SendLine(string.Format("{0}DEVICE,{1},{2},{3},{4}", Set, IntegrationId, SceneController, "7", scene.ID));
        }

        /// <summary>
        /// Appends the delimiter and sends the string
        /// </summary>
        /// <param name="s"></param>
        public void SendLine(string s)
        {
            Communication.SendText(s + Delimiter);
        }

        public void QueryAllDevices()
        {
            SendLine("?DETAILS,FFFFFFFF");
        }

        public void QueryAllIntegrationIds()
        {
            SendLine("?INTEGRATIONID,3");
        }
    }

    public class LutronQSGrafikEyeDeviceFactory : EssentialsDeviceFactory<LutronQS>
    {
        public LutronQSGrafikEyeDeviceFactory()
        {
            TypeNames = new List<string>() { "lutronqsgrafikeye" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lutron QS GrafikEye Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            LutronQuantumPropertiesConfig props =
                Newtonsoft.Json.JsonConvert.DeserializeObject<Environment.Lutron.LutronQuantumPropertiesConfig>(
                    dc.Properties.ToString());

            return new LutronQS(dc.Key, dc.Name, comm, props);
        }
    }
}