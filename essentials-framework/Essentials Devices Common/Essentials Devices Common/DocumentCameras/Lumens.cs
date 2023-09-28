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

namespace PepperDash.Essentials.Devices.Common.Lumens
{
    public class LumensDocumentCameraDevice : EssentialsBridgeableDevice
    {
        public IBasicCommunication Communication { get; private set; }
        public GenericCommunicationMonitor CommunicationMonitor { get; private set; }

        public LumensDocumentCameraDevice(string key, string name, IBasicCommunication comm, DeviceConfig dc)
            : base(key, name)
        {
            PowerIsOnFeedback = new BoolFeedback(() => _PowerIsOn);
            Communication = comm;
            Communication.BytesReceived += Communication_BytesReceived;

            // Custom monitoring, will check the heartbeat tracker count every 20s and reset. Heartbeat sbould be coming in every 30s if subscriptions are valid
            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 30000, 120000, 300000, Poll);
            DeviceManager.AddDevice(CommunicationMonitor);
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return base.CustomActivate();
        }

        /// <summary>
        /// Communication bytes recieved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Event args</param>
        private void Communication_BytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
        }

        #region IBridge Members

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new LumensDocumentCameraJoinMap(joinStart);
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        #endregion


        #region IPower Members

        public void PowerOn()
        {          
        }

        public void PowerOff()
        {         
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

        #region Poll
        public void Poll()
        {

        }

        #endregion

    }

    public class LumensDocumentCameraJoinMap : JoinMapBaseAdvanced
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

        [JoinName("PowerOn")]
        public JoinDataComplete PowerOn = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Power On", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });

        [JoinName("PowerOff")]
        public JoinDataComplete PowerOff = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata { Description = "Power Off", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });


        public LumensDocumentCameraJoinMap(uint joinStart)
            : base(joinStart, typeof(LumensDocumentCameraJoinMap))
        {

        }
    }

    public class LumensDocumentCameraFactory : EssentialsDeviceFactory<LumensDocumentCameraDevice>
    {
        public LumensDocumentCameraFactory()
        {
            TypeNames = new List<string> { "lumensdoccam" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lumens Document Camera device");

            var comms = CommFactory.CreateCommForDevice(dc);

            return new LumensDocumentCameraDevice(dc.Key, dc.Name, comms, dc);
        }
    }
}
