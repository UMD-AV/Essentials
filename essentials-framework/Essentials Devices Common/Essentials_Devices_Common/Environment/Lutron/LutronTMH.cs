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
    public class LutronTMH : LightingBase
    {
        private static readonly byte[][] presets =
        {
            new byte[] { 0xA5, 0x06, 0x85, 0x00, 0xDF, 0xF9 }, // presetOff
            new byte[] { 0xA5, 0x06, 0x85, 0x01, 0xDF, 0xF8 }, // preset1
            new byte[] { 0xA5, 0x06, 0x85, 0x02, 0xDF, 0xFB }, // preset2
            new byte[] { 0xA5, 0x06, 0x85, 0x03, 0xDF, 0xFA }, // preset3
            new byte[] { 0xA5, 0x06, 0x85, 0x04, 0xDF, 0xFD } // preset4
        };

        private string _lastRecalledScene;
        private static readonly byte[] presetFeedback = { 0xA5, 0x06, 0x04, 0x85, 0x5E, 0x7C };
        private static readonly byte[] offFeedback = { 0xA5, 0x06, 0x05, 0x85, 0x5F, 0x7C };

        public IBasicCommunication Communication { get; private set; }

        private LutronGrafikEyePropertiesConfig _props;

        public LutronTMH(string key, string name, IBasicCommunication comm, LutronGrafikEyePropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            _props = props;

            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }

            comm.BytesReceived += CommOnBytesReceived;
        }

        private void CommOnBytesReceived(object sender, GenericCommMethodReceiveBytesArgs e)
        {
            Debug.Console(1, this, "Rx: '{0}'", ComTextHelper.GetEscapedText(e.Bytes));
            if (e.Bytes == presetFeedback)
            {
                CurrentLightingScene = LightingScenes.FirstOrDefault(s => s.ID.Equals(_lastRecalledScene));
            }
            else if (e.Bytes == offFeedback)
            {
                CurrentLightingScene = LightingScenes.FirstOrDefault(s => s.ID.Equals("0"));
            }
        }

        public override bool CustomActivate()
        {
            Communication.Connect();
            return true;
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericLightingJoinMap joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);

            //Not able to monitor this device, no poll string known
            trilist.BooleanInput[joinMap.IsOnline.JoinNumber].BoolValue = true;
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
                if (scene <= 10)
                {
                    Debug.Console(1, this, "Selecting Scene: '{0}'", LightingScenes[scene].ID);
                    _lastRecalledScene = LightingScenes[scene].ID;
                    Communication.SendBytes(presets[int.Parse(LightingScenes[scene].ID)]);
                }
            }
        }
    }

    public class LutronTMHFactory : EssentialsDeviceFactory<LutronTMH>
    {
        public LutronTMHFactory()
        {
            TypeNames = new List<string>() { "lutrontmh" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new Lutron TMH Device");
            IBasicCommunication comm = CommFactory.CreateCommForDevice(dc);

            LutronGrafikEyePropertiesConfig props = Newtonsoft.Json.JsonConvert
                .DeserializeObject<Environment.Lutron.LutronGrafikEyePropertiesConfig>(dc.Properties.ToString());

            return new LutronTMH(dc.Key, dc.Name, comm, props);
        }
    }
}