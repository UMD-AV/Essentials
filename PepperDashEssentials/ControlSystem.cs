using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.Diagnostics;
using Crestron.SimplSharp.Reflection;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.DM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using DynFusion;
using PepperDash_Essentials_Core.Touchpanels;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Core.Touchpanels;
using PepperDash.Essentials.DM.Config;

namespace PepperDash.Essentials
{
    public class ControlSystem : CrestronControlSystem
    {
        private CEvent _initializeEvent;
        private const long StartupTime = 500;

        public ControlSystem()
            : base()
        {
            Thread.MaxNumberOfUserThreads = 400;
            Global.ControlSystem = this;
            DeviceManager.Initialize(this);
            SecretsManager.Initialize();
            SystemMonitor.ProgramInitialization.ProgramInitializationUnderUserControl = true;
        }

        /// <summary>
        /// Entry point for the program
        /// </summary>
        public override void InitializeSystem()
        {
            // If the control system is a DMPS type, we need to wait to exit this method until all devices have had time to activate
            // to allow any HD-BaseT DM endpoints to register first.
            bool preventInitializationComplete = Global.ControlSystemIsDmpsType;
            if (preventInitializationComplete)
            {
                Debug.Console(1, "******************* InitializeSystem() Entering **********************");
                CTimer cTimer = new CTimer(StartSystem, true, StartupTime);
                _initializeEvent = new CEvent(true, false);
                DeviceManager.AllDevicesRegistered += (o, a) => { _initializeEvent.Set(); };
                _initializeEvent.Wait(30000);
                Debug.Console(1, "******************* InitializeSystem() Exiting **********************");
                SystemMonitor.ProgramInitialization.ProgramInitializationComplete = true;
            }
            else
            {
                CTimer cTimer = new CTimer(StartSystem, false, StartupTime);
            }
        }

        private void StartSystem(object preventInitialization)
        {
            DeterminePlatform();

            if (Debug.DoNotLoadOnNextBoot)
            {
                CrestronConsole.AddNewConsoleCommand(s => CrestronInvoke.BeginInvoke((o) => GoWithLoad()), "go",
                    "Loads configuration file",
                    ConsoleAccessLevelEnum.AccessOperator);
            }

            CrestronConsole.AddNewConsoleCommand(s => CrestronInvoke.BeginInvoke((o) => Reload()), "loadconfig",
                "Reloads configuration file at runtime",
                ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(PluginLoader.ReportAssemblyVersions, "reportversions",
                "Reports the versions of the loaded assemblies", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(PepperDash.Essentials.Core.DeviceFactory.GetDeviceFactoryTypes,
                "gettypes", "Gets the device types that can be built. Accepts a filter string.",
                ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(BridgeHelper.PrintJoinMap, "getjoinmap",
                "map(s) for bridge or device on bridge [brKey [devKey]]", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(BridgeHelper.JoinmapMarkdown, "getjoinmapmarkdown"
                , "generate markdown of map(s) for bridge or device on bridge [brKey [devKey]]",
                ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(
                s => Debug.Console(0, Debug.ErrorLogLevel.Notice, "CONSOLE MESSAGE: {0}", s), "appdebugmessage",
                "Writes message to log", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(s =>
                {
                    foreach (TieLine tl in TieLineCollection.Default)
                        CrestronConsole.ConsoleCommandResponse("  {0}\r\n", tl);
                },
                "listtielines", "Prints out all tie lines", ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(s =>
            {
                CrestronConsole.ConsoleCommandResponse
                    ("Current running configuration. This is the merged system and template configuration");
                CrestronConsole.ConsoleCommandResponse(JsonConvert.SerializeObject
                    (ConfigReader.ConfigObject, Formatting.Indented));
            }, "showconfig", "Shows the current running merged config", ConsoleAccessLevelEnum.AccessOperator);


            CrestronConsole.AddNewConsoleCommand(DeviceManager.GetRoutingPorts,
                "getroutingports", "Reports all routing ports, if any.  Requires a device key",
                ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(RouterMain.SetDebug, "routerdebug",
                "Sets the router debug level. Use device key (ex room01-router) to enable and 'off' to disable all. 'fake' will enable fake feedback",
                ConsoleAccessLevelEnum.AccessOperator);

            if (!Debug.DoNotLoadOnNextBoot)
            {
                GoWithLoad();
                return;
            }

            if (!(bool)preventInitialization)
            {
                SystemMonitor.ProgramInitialization.ProgramInitializationComplete = true;
            }
        }

        /// <summary>
        /// Determines if the program is running on a processor (appliance) or server (VC-4).
        /// 
        /// Sets Global.FilePathPrefix and Global.ApplicationDirectoryPathPrefix based on a platform
        /// </summary>
        private void DeterminePlatform()
        {
            try
            {
                string filePathPrefix;

                char dirSeparator = Global.DirectorySeparator;

                string directoryPrefix = Directory.GetApplicationRootDirectory();

                object[] fullVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);

                AssemblyInformationalVersionAttribute fullVersionAtt =
                    fullVersion[0] as AssemblyInformationalVersionAttribute;
                if (fullVersionAtt != null) Global.SetAssemblyVersion(fullVersionAtt.InformationalVersion);

                if (CrestronEnvironment.DevicePlatform !=
                    eDevicePlatform.Server) // Handles 3-series running Windows CE OS
                {
                    string userFolder;
                    bool is4series = false;

                    if (eCrestronSeries.Series4 ==
                        (Global.ProcessorSeries & eCrestronSeries.Series4)) // Handle 4-series
                    {
                        is4series = true;
                        // Set path to user/
                        userFolder = "user";
                    }
                    else
                    {
                        userFolder = "User";
                    }

                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials v{0} on {1} Appliance",
                        Global.AssemblyVersion, is4series ? "4-series" : "3-series");

                    filePathPrefix = directoryPrefix + dirSeparator + userFolder + dirSeparator;
                }
                else // Handles Linux OS (Virtual Control)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials v{0} on Virtual Control Server",
                        Global.AssemblyVersion);

                    // Set path to User/
                    filePathPrefix = directoryPrefix + dirSeparator + "User" + dirSeparator;
                }

                Global.SetFilePathPrefix(filePathPrefix);
            }
            catch (Exception e)
            {
                Debug.Console(0, "Unable to Determine Platform due to Exception: {0}", e.Message);
            }
        }

        /// <summary>
        /// Begins the process of loading resources including plugins and configuration data
        /// </summary>
        private void GoWithLoad()
        {
            try
            {
                Debug.SetDoNotLoadOnNextBoot(false);

                PluginLoader.AddProgramAssemblies();

                Core.DeviceFactory deviceFactory = new Core.DeviceFactory();
                Devices.Common.DeviceFactory factory = new Devices.Common.DeviceFactory();
                DM.DeviceFactory factory1 = new DM.DeviceFactory();
                DeviceFactory deviceFactory1 = new DeviceFactory();

                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials load from configuration");

                bool filesReady = SetupFilesystem();
                if (filesReady)
                {
                    PluginLoader.LoadPlugins();
                    if (!ConfigReader.LoadConfig())
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Error, "Essentials Load complete with errors");
                        return;
                    }

                    Load();
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Essentials load complete\r\n" +
                                                                 "-------------------------------------------------------------");
                }
                else
                {
                    Debug.Console(0,
                        @"----------------------------------------------
                        ------------------------------------------------
                        ------------------------------------------------
                        Essentials file structure setup completed.
                        Please load config, sgd and ir files and
                        restart program.
                        ------------------------------------------------
                        ------------------------------------------------
                        ------------------------------------------------");
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, "FATAL INITIALIZE ERROR. System is in an inconsistent state:\r\n{0}", e);
            }
            finally
            {
                // Notify the OS that the program initialization has completed
                SystemMonitor.ProgramInitialization.ProgramInitializationComplete = true;
            }
        }

        /// <summary>
        /// Verifies filesystem is set up. IR, SGD, and programX folders
        /// </summary>
        private bool SetupFilesystem()
        {
            Debug.Console(0, "Verifying and/or creating folder structure");
            string configDir = Global.FilePathPrefix;
            bool configExists = Directory.Exists(configDir);
            if (!configExists)
                Directory.Create(configDir);

            return configExists;
        }

        /// <summary>
        /// 
        /// </summary>
        public void TearDown()
        {
            Debug.Console(0, "Tearing down existing system");
            DeviceManager.DeactivateAll();

            TieLineCollection.Default.Clear();

            foreach (IKeyed key in DeviceManager.GetDevices())
                DeviceManager.RemoveDevice(key);

            Debug.Console(0, "Tear down COMPLETE");
        }

        /// <summary>
        /// 
        /// </summary>
        private void Load()
        {
            LoadDevices();
            DeviceManager.ActivateAll();
        }

        private void Reload()
        {
            string jsonString = JsonConvert.SerializeObject(ConfigReader.ConfigObject);
            BasicConfig oldConfig = JsonConvert.DeserializeObject<BasicConfig>(jsonString);

            if (!ConfigReader.LoadConfig())
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Config reload has errors");
                return;
            }

            //Delete devices that no longer exist in new config
            foreach (DeviceConfig oldDev in oldConfig.Devices)
            {
                DeviceConfig newDev = ConfigReader.ConfigObject.Devices.Find((d) => d.Key == oldDev.Key);
                if (newDev != null)
                {
                    continue;
                }

                Debug.Console(0, "Device changed or does not exist in new config, deleting: {0}", oldDev.Key);

                IKeyed dev = DeviceManager.GetDeviceForKey(oldDev.Key);
                IDisposable disposable = dev as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                DeviceManager.RemoveDevice(dev);
            }

            //Add new devices. If a match already exists, delete and recreate it
            foreach (DeviceConfig newDev in ConfigReader.ConfigObject.Devices)
            {
                if (newDev.Key.StartsWith("processor"))
                {
                    continue;
                }

                DeviceConfig oldDev = oldConfig.Devices.Find((d) => d.Key == newDev.Key);
                if (oldDev != null)
                {
                    string oldDevString = JsonConvert.SerializeObject(oldDev);
                    string newDevString = JsonConvert.SerializeObject(newDev);
                    if (newDevString == oldDevString)
                    {
                        continue;
                    }

                    Debug.Console(0, "Device changed, deleting: {0}", oldDev.Key);

                    IKeyed dev = DeviceManager.GetDeviceForKey(oldDev.Key);
                    IDisposable disposable = dev as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }

                    DeviceManager.RemoveDevice(dev);
                    List<IKeyed> childDevices =
                        DeviceManager.AllDevices.FindAll((d) => d.Key.StartsWith(oldDev.Key + "-"));
                    foreach (IKeyed child in childDevices)
                    {
                        Debug.Console(0, "Device changed, deleting: {0}", child.Key);
                        DeviceManager.RemoveDevice(child);
                    }
                }

                LoadNonProcessorDevice(newDev);

                IKeyed newDevice = DeviceManager.GetDeviceForKey(newDev.Key);
                if (newDevice is Device)
                {
                    (newDevice as Device).PreActivate();
                    (newDevice as Device).Activate();
                    (newDevice as Device).PostActivate();
                }
            }

            //Remove old rooms or refresh
            foreach (RoomConfig oldRoom in oldConfig.Rooms)
            {
                IKeyed oldRoomDevice = DeviceManager.GetDeviceForKey(oldRoom.Key);
                RoomConfig newRoom = ConfigReader.ConfigObject.Rooms.Find((d) => d.Key == oldRoom.Key);
                if (newRoom != null)
                {
                    Debug.Console(0, "Room match found, refreshing: {0}", oldRoom.Key);
                    ((Room)oldRoomDevice).RefreshConfig();
                    continue;
                }

                Debug.Console(0, "Room does not exist in new config, deleting: {0}", oldRoom.Key);
                DeviceManager.RemoveDevice(oldRoomDevice);
            }

            //Add new rooms
            foreach (RoomConfig newRoomConf in ConfigReader.ConfigObject.Rooms)
            {
                RoomConfig newRoom = oldConfig.Rooms.Find((d) => d.Key == newRoomConf.Key);
                if (newRoom != null)
                {
                    continue;
                }

                Debug.Console(0, "New room found, adding: {0}", newRoomConf.Key);
                Room newRoomDevice = new Room(newRoomConf);
                DeviceManager.AddDevice(newRoomDevice);
            }

            //Remove old uis or refresh
            foreach (UiConfig oldUi in oldConfig.UIs)
            {
                IKeyed oldUiDevice = DeviceManager.GetDeviceForKey(oldUi.Key);
                UiConfig newUi = ConfigReader.ConfigObject.UIs.Find((d) => d.Key == oldUi.Key);
                if (newUi != null)
                {
                    Debug.Console(0, "UI match found, refreshing: {0}", oldUi.Key);
                    ((UI)oldUiDevice).RefreshConfig();
                    continue;
                }

                Debug.Console(0, "UI does not exist in new config, deleting: {0}", oldUi.Key);
                DeviceManager.RemoveDevice(oldUiDevice);
            }

            //Add new uis
            foreach (UiConfig newUiConf in ConfigReader.ConfigObject.UIs)
            {
                UiConfig newUi = oldConfig.UIs.Find((d) => d.Key == newUiConf.Key);
                if (newUi != null)
                {
                    continue;
                }

                Debug.Console(0, "New ui found, adding: {0}", newUiConf.Key);
                UI newUiDevice = new UI(newUiConf);
                DeviceManager.AddDevice(newUiDevice);
            }
        }

        private void LoadNonProcessorDevice(DeviceConfig devConf)
        {
            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating device '{0}', type '{1}'", devConf.Key,
                devConf.Type);

            // Try local factories first
            IKeyed newDev = PepperDash.Essentials.Core.DeviceFactory.GetDevice(devConf);

            if (newDev != null)
            {
                if (devConf.Type.ToLower() == "fusion")
                {
                    Debug.Console(0, "Found fusion device, trying to get embedded resource file");
                    DynFusionDevice fusionDev = newDev as DynFusionDevice;
                    if (fusionDev != null)
                    {
                        fusionDev.customResourceConfig = Encoding.GetEncoding(28591)
                            .GetString(PepperDashEssentials.Properties.Resources.dynFusionCustomAttributes, 0,
                                PepperDashEssentials.Properties.Resources.dynFusionCustomAttributes.Length);
                        Debug.Console(0, "Got fusion embedded resource file");
                    }
                }

                DeviceManager.AddDevice(newDev);
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error,
                    "ERROR: Cannot load unknown device type '{0}', key '{1}'.", devConf.Type, devConf.Key);
            }
        }

        /// <summary>
        /// Reads all devices from config and adds them to DeviceManager
        /// </summary>
        private void LoadDevices()
        {
            // Build the processor wrapper class
            DeviceManager.AddDevice(new PepperDash.Essentials.Core.Devices.CrestronProcessor("processor"));

            // Add global System Monitor device
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                DeviceManager.AddDevice(
                    new Core.Monitoring.SystemMonitorController("systemMonitor"));
            }

            // Add devices
            foreach (DeviceConfig devConf in ConfigReader.ConfigObject.Devices)
            {
                try
                {
                    // Skip this to prevent unnecessary warnings
                    if (devConf.Key == "processor")
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating device '{0}', type '{1}'", devConf.Key,
                            devConf.Type);
                        string prompt = Global.ControlSystem.ControllerPrompt;

                        bool typeMatch = string.Equals(devConf.Type, prompt, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(devConf.Type, prompt.Replace("-", ""),
                                             StringComparison.OrdinalIgnoreCase);

                        if (!typeMatch)
                            Debug.Console(0,
                                "WARNING: Config file defines processor type as '{0}' but actual processor is '{1}'!  Some ports may not be available",
                                devConf.Type.ToUpper(), Global.ControlSystem.ControllerPrompt.ToUpper());

                        // Check if the processor is a DMPS model
                        if (ControllerPrompt.IndexOf("dmps", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            Debug.Console(2, "Adding DmpsRoutingController for {0} to Device Manager.",
                                ControllerPrompt);

                            DmpsRoutingPropertiesConfig propertiesConfig =
                                JsonConvert.DeserializeObject<DmpsRoutingPropertiesConfig>(devConf.Properties
                                    .ToString()) ?? new DmpsRoutingPropertiesConfig();

                            DeviceManager.AddDevice(DmpsRoutingController.GetDmpsRoutingController("switcher01",
                                ControllerPrompt, propertiesConfig));
                        }
                        else if (ControllerPrompt.IndexOf("mpc3", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            Debug.Console(2, "MPC3 processor type detected.  Adding Mpc3TouchpanelController.");

                            JToken butToken = devConf.Properties["buttons"];
                            if (butToken != null)
                            {
                                Dictionary<string, KeypadButton> buttons = butToken
                                    .ToObject<Dictionary<string, KeypadButton>>();
                                Mpc3TouchpanelController tpController =
                                    new Mpc3TouchpanelController(devConf.Key,
                                        devConf.Name, Global.ControlSystem, buttons);
                                DeviceManager.AddDevice(tpController);
                            }
                            else
                            {
                                Debug.Console(0, Debug.ErrorLogLevel.Error,
                                    "Error: Unable to deserialize buttons collection for device: {0}", devConf.Key);
                            }
                        }
                        else
                        {
                            Debug.Console(2, "************Processor is not DMPS type***************");
                        }

                        continue;
                    }

                    LoadNonProcessorDevice(devConf);
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Creating device {0}. Skipping device. \r{1}",
                        devConf.Key, e);
                }
            }

            // Add rooms
            foreach (RoomConfig roomConf in ConfigReader.ConfigObject.Rooms)
            {
                try
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating room '{0}'", roomConf.Key);
                    Room newRoom = new Room(roomConf);
                    DeviceManager.AddDevice(newRoom);
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Creating room {0}. Skipping room. \r{1}",
                        roomConf.Key, e);
                }
            }

            // Add UIs
            foreach (UiConfig uiConf in ConfigReader.ConfigObject.UIs)
            {
                try
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating ui '{0}'", uiConf.Key);
                    UI newUi = new UI(uiConf);
                    DeviceManager.AddDevice(newUi);
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Creating ui {0}. Skipping ui. \r{1}",
                        uiConf.Key, e);
                }
            }

            // Add UMD bridges from the embedded resource file
            string bridges = Encoding.GetEncoding(28591).GetString(PepperDashEssentials.Properties.Resources.umdBridges,
                0, PepperDashEssentials.Properties.Resources.umdBridges.Length);
            BasicConfig BridgesObject = JObject.Parse(bridges).ToObject<BasicConfig>();
            foreach (DeviceConfig devConf in BridgesObject.Devices)
            {
                try
                {
                    // Try local factories first
                    IKeyed newDev = null ?? PepperDash.Essentials.Core.DeviceFactory.GetDevice(devConf);

                    if (newDev != null)
                        DeviceManager.AddDevice(newDev);
                    else
                        Debug.Console(0, Debug.ErrorLogLevel.Error,
                            "ERROR: Cannot load unknown device type '{0}', key '{1}'.", devConf.Type, devConf.Key);
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Creating device {0}. Skipping device. \r{1}",
                        devConf.Key, e);
                }
            }
        }
    }
}