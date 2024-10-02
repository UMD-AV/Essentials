using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Reflection;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials
{
    /// <summary>
    /// Deals with loading plugins at runtime
    /// </summary>
    public static class PluginLoader
    {
        /// <summary>
        /// The complete list of loaded assemblies. Includes Essentials Framework assemblies and plugins
        /// </summary>
        public static List<LoadedAssembly> LoadedAssemblies { get; private set; }

        /// <summary>
        /// The list of assemblies loaded from the plugins folder
        /// </summary>
        private static List<LoadedAssembly> LoadedPluginFolderAssemblies;

        /// <summary>
        /// The directory to look in for .cplz plugin packages
        /// </summary>
        private static string _pluginDirectory = Global.FilePathPrefix + "plugins";

        /// <summary>
        /// The directory where plugins will be moved to and loaded from
        /// </summary>
        private static string _loadedPluginsDirectoryPath =
            _pluginDirectory + Global.DirectorySeparator + "loadedAssemblies";

        // The temp directory where .cplz archives will be unzipped to
        private static string _tempDirectory = _pluginDirectory + Global.DirectorySeparator + "temp";


        static PluginLoader()
        {
            LoadedAssemblies = new List<LoadedAssembly>();
            LoadedPluginFolderAssemblies = new List<LoadedAssembly>();
        }

        /// <summary>
        /// Retrieves all the loaded assemblies from the program directory
        /// </summary>
        public static void AddProgramAssemblies()
        {
            Debug.Console(2, "Getting Assemblies loaded with Essentials");
            // Get the loaded assembly filenames
            DirectoryInfo appDi = new DirectoryInfo(Global.ApplicationDirectoryPathPrefix);
            FileInfo[] assemblyFiles = appDi.GetFiles("*.dll");

            Debug.Console(2, "Found {0} Assemblies", assemblyFiles.Length);

            foreach (FileInfo fi in assemblyFiles)
            {
                string version = string.Empty;
                Assembly assembly = null;

                switch (fi.Name)
                {
                    case ("PepperDashEssentials.dll"):
                    {
                        version = Global.AssemblyVersion;
                        break;
                    }
                    case ("PepperDash_Essentials_Core.dll"):
                    {
                        version = Global.AssemblyVersion;
                        break;
                    }
                    case ("PepperDash_Essentials_DM.dll"):
                    {
                        version = Global.AssemblyVersion;
                        break;
                    }
                    case ("Essentials Devices Common.dll"):
                    {
                        version = Global.AssemblyVersion;
                        break;
                    }
                    case ("PepperDash_Core.dll"):
                    {
                        version = Debug.PepperDashCoreVersion;
                        break;
                    }
                }

                LoadedAssemblies.Add(new LoadedAssembly(fi.Name, version, assembly));
            }

            if (Debug.Level > 1)
            {
                Debug.Console(2, "Loaded Assemblies:");

                foreach (LoadedAssembly assembly in LoadedAssemblies)
                {
                    Debug.Console(2, "Assembly: {0}", assembly.Name);
                }
            }
        }


        public static void SetEssentialsAssembly(string name, Assembly assembly)
        {
            LoadedAssembly loadedAssembly = LoadedAssemblies.FirstOrDefault(la => la.Name.Equals(name));

            if (loadedAssembly != null)
            {
                loadedAssembly.SetAssembly(assembly);
            }
        }

        /// <summary>
        /// Loads an assembly via Reflection and adds it to the list of loaded assemblies
        /// </summary>
        /// <param name="fileName"></param>
        private static LoadedAssembly LoadAssembly(string filePath)
        {
            Assembly assembly = Assembly.LoadFrom(filePath);
            if (assembly != null)
            {
                string assyVersion = GetAssemblyVersion(assembly);

                LoadedAssembly loadedAssembly = new LoadedAssembly(assembly.GetName().Name, assyVersion, assembly);
                LoadedAssemblies.Add(loadedAssembly);
                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Loaded assembly '{0}', version {1}", loadedAssembly.Name,
                    loadedAssembly.Version);
                return loadedAssembly;
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Unable to load assembly: '{0}'", filePath);
            }

            return null;
        }

        /// <summary>
        /// Attempts to get the assembly informational version and if not possible gets the version
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static string GetAssemblyVersion(Assembly assembly)
        {
            object[] ver = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            if (ver != null && ver.Length > 0)
            {
                // Get the AssemblyInformationalVersion              
                AssemblyInformationalVersionAttribute verAttribute = ver[0] as AssemblyInformationalVersionAttribute;
                return verAttribute.InformationalVersion;
            }
            else
            {
                // Get the AssemblyVersion
                Version version = assembly.GetName().Version;
                string verStr = string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build,
                    version.Revision);
                return verStr;
            }
        }

        /// <summary>
        /// Checks if the filename matches an already loaded assembly file's name
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if file already matches loaded assembly file.</returns>
        public static bool CheckIfAssemblyLoaded(string name)
        {
            Debug.Console(2, "Checking if assembly: {0} is loaded...", name);
            LoadedAssembly loadedAssembly = LoadedAssemblies.FirstOrDefault(s => s.Name.Equals(name));

            if (loadedAssembly != null)
            {
                Debug.Console(2, "Assembly already loaded.");
                return true;
            }
            else
            {
                Debug.Console(2, "Assembly not loaded.");
                return false;
            }
        }

        /// <summary>
        /// Used by console command to report the currently loaded assemblies and versions
        /// </summary>
        /// <param name="command"></param>
        public static void ReportAssemblyVersions(string command)
        {
            Debug.Console(0, "Loaded Assemblies:");
            foreach (LoadedAssembly assembly in LoadedAssemblies)
            {
                Debug.Console(0, "{0} Version: {1}", assembly.Name, assembly.Version);
            }
        }

        /// <summary>
        /// Moves any .dll assemblies not already loaded from the plugins folder to loadedPlugins folder
        /// </summary>
        private static void MoveDllAssemblies()
        {
            Debug.Console(0, "Looking for .dll assemblies from plugins folder...");

            DirectoryInfo pluginDi = new DirectoryInfo(_pluginDirectory);
            FileInfo[] pluginFiles = pluginDi.GetFiles("*.dll");

            if (pluginFiles.Length > 0)
            {
                if (!Directory.Exists(_loadedPluginsDirectoryPath))
                {
                    Directory.CreateDirectory(_loadedPluginsDirectoryPath);
                }
            }

            foreach (FileInfo pluginFile in pluginFiles)
            {
                try
                {
                    Debug.Console(0, "Found .dll: {0}", pluginFile.Name);

                    if (!CheckIfAssemblyLoaded(pluginFile.Name))
                    {
                        string filePath = string.Empty;

                        filePath = _loadedPluginsDirectoryPath + Global.DirectorySeparator + pluginFile.Name;

                        // Check if there is a previous file in the loadedPlugins directory and delete
                        if (File.Exists(filePath))
                        {
                            Debug.Console(0,
                                "Found existing file in loadedPlugins: {0} Deleting and moving new file to replace it",
                                filePath);
                            File.Delete(filePath);
                        }

                        // Move the file
                        File.Move(pluginFile.FullName, filePath);
                        Debug.Console(2, "Moved {0} to {1}", pluginFile.FullName, filePath);
                    }
                    else
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Notice,
                            "Skipping assembly: {0}.  There is already an assembly with that name loaded.",
                            pluginFile.FullName);
                    }
                }
                catch (Exception e)
                {
                    Debug.Console(2, "Error with plugin file {0} . Exception: {1}", pluginFile.FullName, e);
                    continue; //catching any load issues and continuing. There will be exceptions loading Crestron .dlls from the cplz Probably should do something different here
                }
            }

            Debug.Console(0, "Done with .dll assemblies");
        }

        /// <summary>
        /// Unzips each .cplz archive into the temp directory and moves any unloaded files into loadedPlugins
        /// </summary>
        private static void UnzipAndMoveCplzArchives()
        {
            Debug.Console(0, "Looking for .cplz archives from plugins folder...");
            DirectoryInfo di = new DirectoryInfo(_pluginDirectory);
            FileInfo[] zFiles = di.GetFiles("*.cplz");

            if (zFiles.Length > 0)
            {
                if (!Directory.Exists(_loadedPluginsDirectoryPath))
                {
                    Directory.CreateDirectory(_loadedPluginsDirectoryPath);
                }
            }

            foreach (FileInfo zfi in zFiles)
            {
                Directory.CreateDirectory(_tempDirectory);
                DirectoryInfo tempDi = new DirectoryInfo(_tempDirectory);

                Debug.Console(0, "Found cplz: {0}. Unzipping into temp plugins directory", zfi.Name);
                CrestronZIP.ResultCode result = CrestronZIP.Unzip(zfi.FullName, tempDi.FullName);
                Debug.Console(0, "UnZip Result: {0}", result.ToString());

                FileInfo[] tempFiles = tempDi.GetFiles("*.dll");
                foreach (FileInfo tempFile in tempFiles)
                {
                    try
                    {
                        if (!CheckIfAssemblyLoaded(tempFile.Name))
                        {
                            string filePath = string.Empty;

                            filePath = _loadedPluginsDirectoryPath + Global.DirectorySeparator + tempFile.Name;

                            // Check if there is a previous file in the loadedPlugins directory and delete
                            if (File.Exists(filePath))
                            {
                                Debug.Console(0,
                                    "Found existing file in loadedPlugins: {0} Deleting and moving new file to replace it",
                                    filePath);
                                File.Delete(filePath);
                            }

                            // Move the file
                            File.Move(tempFile.FullName, filePath);
                            Debug.Console(2, "Moved {0} to {1}", tempFile.FullName, filePath);
                        }
                        else
                        {
                            Debug.Console(0, Debug.ErrorLogLevel.Notice,
                                "Skipping assembly: {0}.  There is already an assembly with that name loaded.",
                                tempFile.FullName);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Console(2, "Assembly {0} is not a custom assembly. Exception: {1}", tempFile.FullName, e);
                        continue; //catching any load issues and continuing. There will be exceptions loading Crestron .dlls from the cplz Probably should do something different here
                    }
                }

                // Delete the .cplz and the temp directory
                Directory.Delete(_tempDirectory, true);
                zfi.Delete();
            }

            Debug.Console(0, "Done with .cplz archives");
        }

        /// <summary>
        /// Attempts to load the assemblies from the loadedPlugins folder
        /// </summary>
        private static void LoadPluginAssemblies()
        {
            Debug.Console(0, "Loading assemblies from loadedPlugins folder...");
            DirectoryInfo pluginDi = new DirectoryInfo(_loadedPluginsDirectoryPath);
            FileInfo[] pluginFiles = pluginDi.GetFiles("*.dll");

            Debug.Console(2, "Found {0} plugin assemblies to load", pluginFiles.Length);

            foreach (FileInfo pluginFile in pluginFiles)
            {
                LoadedAssembly loadedAssembly = LoadAssembly(pluginFile.FullName);

                LoadedPluginFolderAssemblies.Add(loadedAssembly);
            }

            Debug.Console(0, "All Plugins Loaded.");
        }

        /// <summary>
        /// Iterate the loaded assemblies and try to call the LoadPlugin method
        /// </summary>
        private static void LoadCustomPluginTypes()
        {
            Debug.Console(0, "Loading Custom Plugin Types...");
            foreach (LoadedAssembly loadedAssembly in LoadedPluginFolderAssemblies)
            {
                // iteratate this assembly's classes, looking for "LoadPlugin()" methods
                try
                {
                    Assembly assy = loadedAssembly.Assembly;
                    CType[] types = { };
                    try
                    {
                        types = assy.GetTypes();
                    }
                    catch (TypeLoadException e)
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Warning, "Unable to get types for assembly {0}: {1}",
                            loadedAssembly.Name, e.Message);
                        Debug.Console(2, e.StackTrace);
                        continue;
                    }

                    foreach (CType type in types)
                    {
                        try
                        {
                            if (typeof(IPluginDeviceFactory).IsAssignableFrom(type) && !type.IsAbstract)
                            {
                                IPluginDeviceFactory plugin =
                                    (IPluginDeviceFactory)Crestron.SimplSharp.Reflection.Activator.CreateInstance(type);
                                LoadCustomPlugin(plugin, loadedAssembly);
                            }
                            else
                            {
                                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                                MethodInfo loadPlugin = methods.FirstOrDefault(m => m.Name.Equals("LoadPlugin"));
                                if (loadPlugin != null)
                                {
                                    LoadCustomLegacyPlugin(type, loadPlugin, loadedAssembly);
                                }
                            }
                        }
                        catch (NotSupportedException)
                        {
                            //this happens for dlls that aren't PD dlls, like ports of Mono classes into S#. Swallowing.
                        }
                        catch (Exception e)
                        {
                            Debug.Console(2, "Load Plugin not found. {0}.{2} is not a plugin factory. Exception: {1}",
                                loadedAssembly.Name, e.Message, type.Name);
                            continue;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Warning, "Error Loading assembly {0}: {1}",
                        loadedAssembly.Name, e.Message);
                    Debug.Console(2, "{0}", e.StackTrace);
                    continue;
                }
            }

            // plugin dll will be loaded.  Any classes in plugin should have a static constructor
            // that registers that class with the Core.DeviceFactory
            Debug.Console(0, "Done Loading Custom Plugin Types.");
        }

        /// <summary>
        /// Loads a
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="loadedAssembly"></param>
        private static void LoadCustomPlugin(IPluginDeviceFactory plugin, LoadedAssembly loadedAssembly)
        {
            IPluginDevelopmentDeviceFactory developmentPlugin = plugin as IPluginDevelopmentDeviceFactory;

            bool passed = developmentPlugin != null
                ? Global.IsRunningDevelopmentVersion
                (developmentPlugin.DevelopmentEssentialsFrameworkVersions,
                    developmentPlugin.MinimumEssentialsFrameworkVersion)
                : Global.IsRunningMinimumVersionOrHigher(plugin.MinimumEssentialsFrameworkVersion);

            if (!passed)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error,
                    "\r\n********************\r\n\tPlugin indicates minimum Essentials version {0}.  Dependency check failed.  Skipping Plugin {1}\r\n********************",
                    plugin.MinimumEssentialsFrameworkVersion, loadedAssembly.Name);
                return;
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Notice,
                    "Passed plugin passed dependency check (required version {0})",
                    plugin.MinimumEssentialsFrameworkVersion);
            }

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Loading plugin: {0}", loadedAssembly.Name);
            plugin.LoadTypeFactories();
        }

        /// <summary>
        /// Loads a a custom plugin via the legacy method
        /// </summary>
        /// <param name="type"></param>
        /// <param name="loadPlugin"></param>
        private static void LoadCustomLegacyPlugin(CType type, MethodInfo loadPlugin, LoadedAssembly loadedAssembly)
        {
            Debug.Console(2, "LoadPlugin method found in {0}", type.Name);

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

            FieldInfo minimumVersion = fields.FirstOrDefault(p => p.Name.Equals("MinimumEssentialsFrameworkVersion"));
            if (minimumVersion != null)
            {
                Debug.Console(2, "MinimumEssentialsFrameworkVersion found");

                string minimumVersionString = minimumVersion.GetValue(null) as string;

                if (!string.IsNullOrEmpty(minimumVersionString))
                {
                    bool passed = Global.IsRunningMinimumVersionOrHigher(minimumVersionString);

                    if (!passed)
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Error,
                            "Plugin indicates minimum Essentials version {0}.  Dependency check failed.  Skipping Plugin",
                            minimumVersionString);
                        return;
                    }
                    else
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Notice,
                            "Passed plugin passed dependency check (required version {0})", minimumVersionString);
                    }
                }
                else
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Warning,
                        "MinimumEssentialsFrameworkVersion found but not set.  Loading plugin, but your mileage may vary.");
                }
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Warning,
                    "MinimumEssentialsFrameworkVersion not found.  Loading plugin, but your mileage may vary.");
            }

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Loading legacy plugin: {0}", loadedAssembly.Name);
            loadPlugin.Invoke(null, null);
        }

        /// <summary>
        /// Loads plugins
        /// </summary>
        public static void LoadPlugins()
        {
            if (Directory.Exists(_pluginDirectory))
            {
                // Deal with any .dll files
                MoveDllAssemblies();

                // Deal with any .cplz files
                UnzipAndMoveCplzArchives();

                if (Directory.Exists(_loadedPluginsDirectoryPath))
                {
                    // Load the assemblies from the loadedPlugins folder into the AppDomain
                    LoadPluginAssemblies();

                    // Load the types from any custom plugin assemblies
                    LoadCustomPluginTypes();
                }
            }
        }
    }

    /// <summary>
    /// Represents an assembly loaded at runtime and it's associated metadata
    /// </summary>
    public class LoadedAssembly
    {
        public string Name { get; private set; }
        public string Version { get; private set; }
        public Assembly Assembly { get; private set; }

        public LoadedAssembly(string name, string version, Assembly assembly)
        {
            Name = name;
            Version = version;
            Assembly = assembly;
        }

        public void SetAssembly(Assembly assembly)
        {
            Assembly = assembly;
        }
    }
}