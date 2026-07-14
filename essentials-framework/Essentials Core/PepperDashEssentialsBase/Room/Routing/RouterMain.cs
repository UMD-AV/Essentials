using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using PepperDash.Core;

namespace UmdEssentials.Core.Routing
{
    public static class RouterMain
    {
        public const ushort maxSources = 32;
        public const ushort maxDests = 24;
        public const ushort numDisplays = 16;

        private static readonly Dictionary<string, Router> Routers = new Dictionary<string, Router>();
        private static readonly Dictionary<string, RoutingInterface> UIs = new Dictionary<string, RoutingInterface>();
        private static readonly CMutex mutex = new CMutex();

        public static void AddRouter(string key, Router router)
        {
            mutex.WaitForMutex();
            Routers[key] = router;

            //Update all UI connections
            foreach (RoutingInterface ui in UIs.Values)
                if (ui.RouterKey == key)
                    ui.Register(key);

            mutex.ReleaseMutex();
        }

        public static void SetDebug(string command)
        {
            if (command == "off")
            {
                foreach (Router r in Routers.Values)
                {
                    r.debugLevel = 0;
                    r.fakeFeedback = false;
                }

                foreach (RoutingInterface i in UIs.Values) i.debugLevel = 0;
            }
            else if (command == "fake")
            {
                foreach (Router r in Routers.Values) r.fakeFeedback = true;
            }
            else
            {
                if (Routers.ContainsKey(command))
                    Routers[command].debugLevel = 1;
                else if (UIs.ContainsKey(command)) UIs[command].debugLevel = 1;
            }
        }

        public static void AddInterface(string key, RoutingInterface ui)
        {
            mutex.WaitForMutex();
            UIs[key] = ui;

            mutex.ReleaseMutex();
        }

        public static Router GetRouter(string key)
        {
            try
            {
                mutex.WaitForMutex();
                if (Routers.ContainsKey(key)) return Routers[key];
            }
            catch (Exception e)
            {
                Debug.ConsoleWithLog(0, "Exception getting router {0}: {1}", key, e.Message);
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return null;
        }
    }
}