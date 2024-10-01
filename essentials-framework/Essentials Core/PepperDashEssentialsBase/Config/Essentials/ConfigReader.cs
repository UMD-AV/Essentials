﻿using System;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace PepperDash.Essentials.Core.Config
{
    /// <summary>
    /// Loads the ConfigObject from the file
    /// </summary>
    public class ConfigReader
    {
        public const string LocalConfigPresent =
            @"
***************************************************
************* Using Local config file *************
***************************************************";

        public static EssentialsConfig ConfigObject { get; private set; }

        public static bool LoadConfig2()
        {
            try
            {
                // Check for local config file first
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

                // Generate debug statement if using a local file.
                GetLocalFileMessage(filePath);

                // Read the file
                using (StreamReader fs = new StreamReader(filePath))
                {
                    ConfigObject = JObject.Parse(fs.ReadToEnd()).ToObject<EssentialsConfig>();
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Successfully Loaded Config: {0}", filePath);
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
            DeviceConfig dev = ConfigObject.Devices.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            return dev == null ? null : dev.Group;
        }

        private static void GetLocalFileMessage(string filePath)
        {
            int filePathLength = filePath.Length + 2;
            int debugStringWidth = filePathLength + 12;

            if (debugStringWidth < 51)
            {
                debugStringWidth = 51;
            }

            string qualifier = (filePathLength % 2 != 0)
                ? " Using Local Config File "
                : " Using Local  Config File ";
            int bookend1 = (debugStringWidth - qualifier.Length) / 2;
            int bookend2 = (debugStringWidth - filePathLength) / 2;


            StringBuilder newDebugString = new StringBuilder()
                .Append(CrestronEnvironment.NewLine)
                // Line 1
                .Append(new string('*', debugStringWidth))
                .Append(CrestronEnvironment.NewLine)
                // Line 2
                .Append(new string('*', debugStringWidth))
                .Append(CrestronEnvironment.NewLine)
                // Line 3
                .Append(new string('*', 2))
                .Append(new string(' ', debugStringWidth - 4))
                .Append(new string('*', 2))
                .Append(CrestronEnvironment.NewLine)
                // Line 4
                .Append(new string('*', 2))
                .Append(new string(' ', bookend1 - 2))
                .Append(qualifier)
                .Append(new string(' ', bookend1 - 2))
                .Append(new string('*', 2))
                .Append(CrestronEnvironment.NewLine)
                // Line 5
                .Append(new string('*', 2))
                .Append(new string(' ', bookend2 - 2))
                .Append(" " + filePath + " ")
                .Append(new string(' ', bookend2 - 2))
                .Append(new string('*', 2))
                .Append(CrestronEnvironment.NewLine)
                // Line 6
                .Append(new string('*', 2))
                .Append(new string(' ', debugStringWidth - 4))
                .Append(new string('*', 2))
                .Append(CrestronEnvironment.NewLine)
                // Line 7
                .Append(new string('*', debugStringWidth))
                .Append(CrestronEnvironment.NewLine)
                // Line 8
                .Append(new string('*', debugStringWidth));

            Debug.Console(0, newDebugString.ToString());
        }
    }
}