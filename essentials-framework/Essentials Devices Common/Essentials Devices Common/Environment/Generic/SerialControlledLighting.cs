using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Lighting;
using LightingBase = PepperDash.Essentials.Core.Lighting.LightingBase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Devices.Common.Environment.Generic
{
    public class SerialControlledLighting : LightingBase, ICommunicationMonitor
    {
        public IBasicCommunication Communication { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        public SerialControlledLighting(string key, string name, IBasicCommunication comm, SerialControlledLightingPropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;

            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }

            if (props.PollString != null)
            {
                CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 60000, 120000, 300000, props.PollString);
            }
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            CommunicationMonitor.Start();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);

            CommunicationMonitor.IsOnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public override void SelectScene(LightingScene scene)
        {
            if (LightingScenes != null && LightingScenes.Exists(o => o.ID == scene.ID))
            {
                SelectScene((ushort)LightingScenes.FindIndex(o => o.ID == scene.ID));
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
		/// 
        public void SelectScene(ushort scene)
        {
            if (LightingScenes != null && LightingScenes[scene] != null && LightingScenes[scene].ID != null)
            {
                if (scene >= 0 && scene <= 10)
                {
                    Debug.Console(1, this, "Selecting Scene: '{0}'", LightingScenes[scene].ID);
                    if (LightingScenes[scene].Command != null)
                    {
                        Communication.SendText(LightingScenes[scene].Command);
                    }
                }
            }
        }
    }

    public class SerialControlledLightingPropertiesConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }
        public ControlPropertiesConfig Control { get; set; }

        [JsonProperty("pollString", NullValueHandling = NullValueHandling.Ignore)]
        public string PollString { get; set; }

        public List<LightingScene> Scenes { get; set; }
    }

    public class SerialControlledLightingFactory : EssentialsDeviceFactory<SerialControlledLighting>
    {
        public SerialControlledLightingFactory()
        {
            TypeNames = new List<string>() { "seriallighting" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Serial Controlled Lighting Device");
            var comm = CommFactory.CreateCommForDevice(dc);

            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<Environment.Generic.SerialControlledLightingPropertiesConfig>(dc.Properties.ToString());

            return new SerialControlledLighting(dc.Key, dc.Name, comm, props);
        }
    }

}