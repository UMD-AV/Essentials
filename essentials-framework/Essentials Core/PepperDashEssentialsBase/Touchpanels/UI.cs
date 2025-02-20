using System;
using System.Linq;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.Routing;

namespace PepperDash_Essentials_Core.Touchpanels
{
    public class UI : IKeyName, IBridgeAdvanced
    {
        public string Key { get; private set; }
        public string Name { get; private set; }
        private UiConfig uiConfig;
        private UiJoinMap joinMap;
        private BasicTriList uiTriList;

        public UI(UiConfig config)
        {
            Key = config.Key;
            Name = config.Name;
            uiConfig = config;

            try
            {
                RoutingInterface routingInterface = new RoutingInterface(config);
                DeviceManager.AddDevice(routingInterface);
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, "Exception creating routing interface {0}: {1}", config.Key, e.Message);
            }
        }

        public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            uiTriList = trilist;
            joinMap = new UiJoinMap(joinStart);
            bridge.AddJoinMap(Key, joinMap);
            UpdateBridge();
        }

        private void UpdateBridge()
        {
            //serial
            uiTriList.StringInput[joinMap.Name.JoinNumber].StringValue = uiConfig.Name;
            uiTriList.StringInput[joinMap.DefaultRoomKey.JoinNumber].StringValue = uiConfig.DefaultRoomKey;
            uiTriList.StringInput[joinMap.UserPassword.JoinNumber].StringValue = uiConfig.UserPassword;
            uiTriList.StringInput[joinMap.TechPassword.JoinNumber].StringValue = uiConfig.TechPassword;
            uiTriList.StringInput[joinMap.ScheduleKey.JoinNumber].StringValue = uiConfig.ScheduleKey;
        }

        public void RefreshConfig()
        {
            uiConfig = ConfigReader.ConfigObject.UIs.First((config) => config.Key == Key);
            Name = uiConfig.Name;
            UpdateBridge();
        }
    }
}