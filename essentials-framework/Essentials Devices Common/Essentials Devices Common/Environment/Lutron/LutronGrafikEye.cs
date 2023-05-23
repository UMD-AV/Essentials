﻿using System;
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

namespace PepperDash.Essentials.Devices.Common.Environment.Lutron
{
    public class LutronGrafikEye : LightingBase, ICommunicationMonitor
    {
        public IBasicCommunication Communication { get; private set; }
        public CommunicationGather PortGather { get; private set; }
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        LutronGrafikEyePropertiesConfig _props;

        private string _controlUnit;
        public string ControlUnit
        {
            get { return _controlUnit; }
            set
            {
                if (_controlUnit == value) return;
                _controlUnit = value;
                UpdateConfigControlUnit(value);
            }
        }

        public LutronGrafikEye(string key, string name, IBasicCommunication comm, LutronGrafikEyePropertiesConfig props)
            : base(key, name)
        {
            Communication = comm;
            _props = props;

            ControlUnit = (props.ControlUnit != null && props.ControlUnit.Length > 0) ? props.ControlUnit : "1";
            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }

            PortGather = new CommunicationGather(Communication, "\x0D\x0A");
            PortGather.LineReceived += new EventHandler<GenericCommMethodReceiveTextArgs>(PortGather_LineReceived);

            CommunicationMonitor = new GenericCommunicationMonitor(this, Communication, 60000, 120000, 300000, ":G\x0d\x0a");
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

        private void UpdateConfigControlUnit(string id)
        {
            if (_props.ControlUnit != id)
            {
                _props.ControlUnit = id;
                //ConfigWriter.UpdateDeviceProperties(this.Key, JToken.FromObject(_props));
            }
        }

        /// <summary>
        /// Handles all responses that contain the delimiter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void PortGather_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            Debug.Console(2, this, "Line Received: '{0}'", args.Text);

            try
            {
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
                    SendLine(string.Format(":A{0}{1}", LightingScenes[scene].ID, ControlUnit));
                }
            }
        }

        /// <summary>
        /// Appends the delimiter and sends the string
        /// </summary>
        /// <param name="s"></param>
        public void SendLine(string s)
        {
            Communication.SendText(s + '\r');
        }
    }

    public class LutronGrafikEyePropertiesConfig
    {
        public CommunicationMonitorConfig CommunicationMonitorProperties { get; set; }
        public ControlPropertiesConfig Control { get; set; }

        public string ControlUnit { get; set; }
        public List<LightingScene> Scenes { get; set; }
    }

    public class LutronGrafikEyeFactory : EssentialsDeviceFactory<LutronGrafikEye>
    {
        public LutronGrafikEyeFactory()
        {
            TypeNames = new List<string>() { "lutrongrafikeye" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new LutronGrafikEye Device");
            var comm = CommFactory.CreateCommForDevice(dc);

            var props = Newtonsoft.Json.JsonConvert.DeserializeObject<Environment.Lutron.LutronGrafikEyePropertiesConfig>(dc.Properties.ToString());

            return new LutronGrafikEye(dc.Key, dc.Name, comm, props);
        }
    }

}