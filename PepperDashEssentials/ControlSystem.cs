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
using System.Net.Sockets;
using DynFusion;
using PepperDash.Essentials.Core.Touchpanels;
using PepperDash.Essentials.DM.Config;

namespace PepperDash.Essentials
{
    public class ControlSystem : CrestronControlSystem
    {
        private CTimer _startTimer;
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
                _startTimer = new CTimer(StartSystem, true, StartupTime);
                _initializeEvent = new CEvent(true, false);
                DeviceManager.AllDevicesRegistered += (o, a) => { _initializeEvent.Set(); };
                _initializeEvent.Wait(30000);
                Debug.Console(1, "******************* InitializeSystem() Exiting **********************");
                SystemMonitor.ProgramInitialization.ProgramInitializationComplete = true;
            }
            else
            {
                _startTimer = new CTimer(StartSystem, false, StartupTime);
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
                CrestronConsole.ConsoleCommandResponse(Newtonsoft.Json.JsonConvert.SerializeObject
                    (ConfigReader.ConfigObject, Newtonsoft.Json.Formatting.Indented));
            }, "showconfig", "Shows the current running merged config", ConsoleAccessLevelEnum.AccessOperator);


            CrestronConsole.AddNewConsoleCommand(DeviceManager.GetRoutingPorts,
                "getroutingports", "Reports all routing ports, if any.  Requires a device key",
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

                string directoryPrefix = Crestron.SimplSharp.CrestronIO.Directory.GetApplicationRootDirectory();

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
                    if (!ConfigReader.LoadConfig2())
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
                    new PepperDash.Essentials.Core.Monitoring.SystemMonitorController("systemMonitor"));
            }

            foreach (DeviceConfig devConf in ConfigReader.ConfigObject.Devices)
            {
                try
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating device '{0}', type '{1}'", devConf.Key,
                        devConf.Type);
                    // Skip this to prevent unnecessary warnings
                    if (devConf.Key == "processor")
                    {
                        string prompt = Global.ControlSystem.ControllerPrompt;

                        bool typeMatch = string.Equals(devConf.Type, prompt, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(devConf.Type, prompt.Replace("-", ""),
                                             StringComparison.OrdinalIgnoreCase);

                        if (!typeMatch)
                            Debug.Console(0,
                                "WARNING: Config file defines processor type as '{0}' but actual processor is '{1}'!  Some ports may not be available",
                                devConf.Type.ToUpper(), Global.ControlSystem.ControllerPrompt.ToUpper());

                        // Check if the processor is a DMPS model
                        if (this.ControllerPrompt.IndexOf("dmps", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            Debug.Console(2, "Adding DmpsRoutingController for {0} to Device Manager.",
                                this.ControllerPrompt);

                            DmpsRoutingPropertiesConfig propertiesConfig =
                                JsonConvert.DeserializeObject<DM.Config.DmpsRoutingPropertiesConfig>(devConf.Properties
                                    .ToString()) ?? new DM.Config.DmpsRoutingPropertiesConfig();

                            DeviceManager.AddDevice(DmpsRoutingController.GetDmpsRoutingController("switcher01",
                                this.ControllerPrompt, propertiesConfig));
                        }
                        else if (this.ControllerPrompt.IndexOf("mpc3", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            Debug.Console(2, "MPC3 processor type detected.  Adding Mpc3TouchpanelController.");

                            JToken butToken = devConf.Properties["buttons"];
                            if (butToken != null)
                            {
                                Dictionary<string, KeypadButton> buttons = butToken
                                    .ToObject<Dictionary<string, Essentials.Core.Touchpanels.KeypadButton>>();
                                Mpc3TouchpanelController tpController =
                                    new Essentials.Core.Touchpanels.Mpc3TouchpanelController(devConf.Key,
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

                    // Try local factories first
                    IKeyed newDev = null ?? PepperDash.Essentials.Core.DeviceFactory.GetDevice(devConf);

                    if (newDev != null)
                    {
                        if (devConf.Type.ToLower() == "dynfusion")
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
                        Debug.Console(0, Debug.ErrorLogLevel.Error,
                            "ERROR: Cannot load unknown device type '{0}', key '{1}'.", devConf.Type, devConf.Key);
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Creating device {0}. Skipping device. \r{1}",
                        devConf.Key, e);
                }
            }

            string bridges = Encoding.GetEncoding(28591).GetString(PepperDashEssentials.Properties.Resources.umdBridges,
                0, PepperDashEssentials.Properties.Resources.umdBridges.Length);
            EssentialsConfig BridgesObject = JObject.Parse(bridges).ToObject<EssentialsConfig>();

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