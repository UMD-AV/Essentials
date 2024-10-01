using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Lighting;
using LightingBase = PepperDash.Essentials.Core.Lighting.LightingBase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace PepperDash.Essentials.Devices.Common.Environment
{
    public class RelayControlledLighting : LightingBase
    {
        RelayControlledLightingPropertiesConfig _props;
        Relay[] relayOutputs;
        CMutex sceneMutex;

        public RelayControlledLighting(string key, string name, RelayControlledLightingPropertiesConfig props)
            : base(key, name)
        {
            _props = props;
            relayOutputs = new Relay[11];
            sceneMutex = new CMutex();
            if (props.Scenes != null)
            {
                LightingScenes = props.Scenes;
            }
        }

        public override bool CustomActivate()
        {
            uint count = 0;
            foreach (LightingScene scene in LightingScenes)
            {
                if (scene.PortDeviceKey != null)
                {
                    relayOutputs[count] = GetRelay(scene.PortDeviceKey, scene.PortNumber);
                    count++;
                }
            }

            return true;
        }

        private Relay GetRelay(string portDeviceKey, uint portNumber)
        {
            IRelayPorts relayDevice;

            if (portDeviceKey.Equals("processor"))
            {
                if (!Global.ControlSystem.SupportsRelay)
                {
                    Debug.Console(0, "Processor does not support relays");
                    return null;
                }

                relayDevice = Global.ControlSystem;
                relayDevice.RelayPorts[portNumber].StateChange +=
                    new RelayEventHandler(RelayControlledLighting_StateChange);
                return relayDevice.RelayPorts[portNumber];
            }

            IKeyed essentialsDevice = DeviceManager.GetDeviceForKey(portDeviceKey);
            if (essentialsDevice == null)
            {
                Debug.Console(0,
                    "Device {0} was not found in Device Manager. Check configuration or for errors with device.",
                    portDeviceKey);
                return null;
            }

            relayDevice = essentialsDevice as IRelayPorts;

            if (relayDevice == null)
            {
                Debug.Console(0, "Device {0} is not a valid relay parent. Please check configuration.", portDeviceKey);
                return null;
            }

            if (portNumber <= relayDevice.NumberOfRelayPorts)
            {
                Debug.Console(1, "Adding relay {0} on device {1}", portNumber, portDeviceKey);
                relayDevice.RelayPorts[portNumber].StateChange +=
                    new RelayEventHandler(RelayControlledLighting_StateChange);
                return relayDevice.RelayPorts[portNumber];
            }

            Debug.Console(0, "Device {0} does not contain a port {1}", portDeviceKey, portNumber);
            return null;
        }

        private void RelayControlledLighting_StateChange(Relay relay, RelayEventArgs args)
        {
            Debug.Console(1, "Relay on device {0} changed to {1}", relay.DeviceName.ToString(), args.State.ToString());
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            GenericLightingJoinMap joinMap = new GenericLightingJoinMap(joinStart);
            LinkLightingToApi(trilist, joinStart, joinMapKey, bridge);
            trilist.BooleanInput[joinMap.IsOnline.JoinNumber].BoolValue = true;
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public override void SelectScene(LightingScene scene)
        {
            if (LightingScenes != null && LightingScenes.Exists(o => o.Name == scene.Name))
            {
                SelectScene((ushort)LightingScenes.FindIndex(o => o.Name == scene.Name));
            }
        }

        /// <summary>
        /// Recalls the specified scene
        /// </summary>
        /// <param name="scene"></param>
        /// 
        public void SelectScene(ushort sceneNum)
        {
            CrestronInvoke.BeginInvoke((o) =>
            {
                if (LightingScenes != null && LightingScenes[sceneNum] != null)
                {
                    bool test = sceneMutex.WaitForMutex(1000);
                    if (test)
                    {
                        try
                        {
                            LightingScene scene = LightingScenes[sceneNum];
                            if (sceneNum >= 0 && sceneNum <= 10 && relayOutputs[sceneNum] != null)
                            {
                                Debug.Console(1, this, "Selecting Scene: '{0}'", scene.Name);
                                relayOutputs[sceneNum].Close();
                                CrestronEnvironment.Sleep(1000);
                                relayOutputs[sceneNum].Open();
                            }
                        }
                        finally
                        {
                            sceneMutex.ReleaseMutex();
                        }
                    }
                }
            });
        }
    }

    public class RelayControlledLightingPropertiesConfig
    {
        public List<LightingScene> Scenes { get; set; }
    }

    public class RelayControlledLightingFactory : EssentialsDeviceFactory<RelayControlledLighting>
    {
        public RelayControlledLightingFactory()
        {
            TypeNames = new List<string>() { "relaylighting" };
        }

        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            Debug.Console(1, "Factory Attempting to create new RelayControlledLighting Device");
            RelayControlledLightingPropertiesConfig props =
                Newtonsoft.Json.JsonConvert.DeserializeObject<RelayControlledLightingPropertiesConfig>(
                    dc.Properties.ToString());

            return new RelayControlledLighting(dc.Key, dc.Name, props);
        }
    }
}