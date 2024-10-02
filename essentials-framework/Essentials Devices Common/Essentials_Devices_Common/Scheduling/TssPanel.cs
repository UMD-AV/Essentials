using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using Noemax.WebSockets;

namespace PepperDash.Essentials.Devices.Common.Scheduling
{
    public class TssPanel : EssentialsDevice, IBridgeAdvanced
    {
        private string _port;
        private WebSocketServer _webSocketServer;

        public TssPanel(string key, string name, TssPanelPropertiesConfig props) :
            base(key, name)
        {
            _port = props.port;
            CrestronEnvironment.ProgramStatusEventHandler += CrestronEnvironmentOnProgramStatusEventHandler;
        }

        public override bool CustomActivate()
        {
            BuildServer();
            return true;
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            TssPanelJoinMap joinMap = new TssPanelJoinMap(joinStart);
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }
        }

        private void BuildServer()
        {
            try
            {
                _webSocketServer = new WebSocketServer();

                _webSocketServer.AddEndpoint<Service>(new Uri("ws://0.0.0.0:" + _port));
                _webSocketServer.Open();

                CrestronConsole.Print("WebSocket server started");
            }
            catch
            {
                Debug.Console(0, this, "Error building websocket server");
            }
        }

        private void CrestronEnvironmentOnProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            if (programEventType != eProgramStatusEventType.Stopping) return;
        }
    }

    public class Service : WebSocketService
    {
        public Service()
        {
            MaxReceivedMessageSize = 1024;
        }

        public override void OnOpen(WebSocketChannel channel)
        {
            channel.MaxOutboundQueueSize = 1024 * 1024;
            // adds the channel to the list of associated channels
            base.OnOpen(channel);
        }

        public override void OnMessage(WebSocketChannel channel, string message)
        {
            // read any type of received message as an outbound message and broadcast irrespective of the message type
            CrestronConsole.Print(message);
        }
    }

    public class TssPanelFactory : EssentialsDeviceFactory<TssPanel>
    {
        public TssPanelFactory()
        {
            TypeNames = new List<string>() { "tsspanel" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory attempting to create new TSS Panel Device");
            TssPanelPropertiesConfig props =
                Newtonsoft.Json.JsonConvert.DeserializeObject<TssPanelPropertiesConfig>(
                    dc.Properties.ToString());
            return new TssPanel(dc.Key, dc.Name, props);
        }
    }

    public class TssPanelPropertiesConfig
    {
        public string port { get; set; }
    }

    public class TssPanelJoinMap : JoinMapBaseAdvanced
    {
        #region Digital

        [JoinName("Online")] public JoinDataComplete Online = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Online Status",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion


        #region Serial

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData()
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata()
            {
                Description = "Device Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        #endregion

        public TssPanelJoinMap(uint joinStart)
            : base(joinStart, typeof(CollegeNetJoinMap))
        {
        }
    }
}