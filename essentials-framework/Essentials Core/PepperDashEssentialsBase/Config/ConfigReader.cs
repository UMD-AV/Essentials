using System;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using PepperDash.Core;

namespace UmdEssentials.Core.Config
{
    /// <summary>
    /// Loads the ConfigObject from the file
    /// </summary>
    public static class ConfigReader
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private static StreamReader fs;
        public static BasicConfig ConfigObject { get; private set; }

        public static bool LoadConfig()
        {
            try
            {
                // Check for a local config file first
                string filePath = Global.FilePathPrefix + Global.ConfigFileName;

                // Check for local config directory first
                FileInfo[] configFiles = GetConfigFiles(filePath);

                if (configFiles != null)
                {
                    if (configFiles.Length > 1)
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Error,
                            "****Error: Multiple configuration files present. Please ensure only a single file exists and reset program.****");
                        return false;
                    }
                }
                else
                {
                    Debug.Console(0, Debug.ErrorLogLevel.Notice,
                        "Configuration file not present.", filePath);
                    return false;
                }

                // Get the actual file path
                filePath = configFiles[0].FullName;
                ConfigWriter.ConfigLocation = filePath;

                // Read the file
                using (fs = new StreamReader(filePath))
                {
                    string config = fs.ReadToEnd();
                    bool replacedPrefix = false;
                    //Get a prefix for this processor such as ESJ-0201 for processor ESJ-0201-CP
                    try
                    {
                        string hostname =
                            CrestronEthernetHelper.GetEthernetParameter(
                                CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0);
                        if (!string.IsNullOrEmpty(hostname) && hostname.LastIndexOf('-') != -1)
                        {
                            string systemPrefix = hostname.Substring(0, hostname.LastIndexOf('-'));
                            Debug.Console(0, "Using system prefix: {0}", systemPrefix);

                            if (config.Contains("{{prefix}}"))
                            {
                                config = config.Replace("{{prefix}}", systemPrefix);
                                replacedPrefix = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Console(0, "Exception getting system prefix {0}", e);
                    }

                    ConfigObject = JsonConvert.DeserializeObject<BasicConfig>(config, _jsonSettings);
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Successfully Loaded Config: {0}", filePath);
                    if (replacedPrefix) ConfigWriter.SaveConfigFile();
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "ERROR: Config load failed: \r{0}", e);
                return false;
            }
        }

        /// <summary>
        /// Returns all the files from the directory specified.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static FileInfo[] GetConfigFiles(string filePath)
        {
            // Get the directory
            string dir = Path.GetDirectoryName(filePath);

            if (Directory.Exists(dir))
            {
                Debug.Console(1, "Searching in Directory '{0}'", dir);
                // Get the directory info
                DirectoryInfo dirInfo = new DirectoryInfo(dir);

                // Get the file name
                string fileName = Path.GetFileName(filePath);
                Debug.Console(1, "For Config Files matching: '{0}'", fileName);

                // Get the files that match from the directory
                return dirInfo.GetFiles(fileName);
            }
            else
            {
                Debug.Console(0, Debug.ErrorLogLevel.Notice,
                    "Directory not found: ", dir);

                return null;
            }
        }

        /// <summary>
        /// Returns the group for a given device key in config
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetGroupForDeviceKey(string key)
        {
            DeviceConfig dev =
                ConfigObject.Devices.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return dev == null ? null : dev.Group;
        }
    }
}