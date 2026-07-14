using System;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace UmdEssentials.Core.Config
{
    /// <summary>
    /// Responsible for updating config at runtime, and writing the updates out to a local file
    /// </summary>
    public static class ConfigWriter
    {
        public static string ConfigLocation;

        public const long WriteTimeout = 30000;

        public static CTimer WriteTimer;
        private static readonly CCriticalSection fileLock = new CCriticalSection();

        /// <summary>
        /// Updates the config properties of a device
        /// </summary>
        /// <param name="deviceKey"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static bool UpdateDeviceProperties(string deviceKey, JToken properties)
        {
            bool success = false;

            // Get the current device config
            DeviceConfig deviceConfig = ConfigReader.ConfigObject.Devices.FirstOrDefault(d => d.Key.Equals(deviceKey));

            if (deviceConfig != null)
            {
                // Replace the current properties JToken with the new one passed into this method
                deviceConfig.Properties = properties;

                Debug.Console(1, "Updated properties of device: '{0}'", deviceKey);

                success = true;
            }

            ResetTimer();

            return success;
        }

        public static bool UpdateDeviceConfig(DeviceConfig config)
        {
            bool success = false;

            int deviceConfigIndex = ConfigReader.ConfigObject.Devices.FindIndex(d => d.Key.Equals(config.Key));

            if (deviceConfigIndex >= 0)
            {
                ConfigReader.ConfigObject.Devices[deviceConfigIndex] = config;

                Debug.Console(1, "Updated config of device: '{0}'", config.Key);

                success = true;
            }

            ResetTimer();

            return success;
        }

        public static bool UpdateRoomConfig(RoomConfig config)
        {
            bool success = false;

            int roomConfigIndex = ConfigReader.ConfigObject.Rooms.FindIndex(d => d.Key.Equals(config.Key));

            if (roomConfigIndex >= 0)
            {
                ConfigReader.ConfigObject.Rooms[roomConfigIndex] = config;

                Debug.Console(1, "Updated room of device: '{0}'", config.Key);

                success = true;
            }

            ResetTimer();

            return success;
        }

        /// <summary>
        /// Resets (or starts) the writer timer
        /// </summary>
        private static void ResetTimer()
        {
            if (WriteTimer == null)
                WriteTimer = new CTimer(WriteConfigFile, WriteTimeout);

            WriteTimer.Reset(WriteTimeout);

            Debug.Console(1, "Config File write timer has been reset.");
        }

        /// <summary>
        /// Saves the current config to file
        /// </summary>
        /// <returns></returns>
        public static void SaveConfigFile()
        {
            ResetTimer();
        }

        /// <summary>
        /// Writes the current config to file
        /// </summary>
        /// <returns></returns>
        private static void WriteConfigFile(object o)
        {
            string configData = JsonConvert.SerializeObject(ConfigReader.ConfigObject, Formatting.Indented,
                new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                });

            WriteFile(ConfigLocation, configData);
        }

        /// <summary>
        /// Writes
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="configData"></param>
        private static void WriteFile(string filePath, string configData)
        {
            if (WriteTimer != null)
                WriteTimer.Stop();

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Writing Configuration to file");

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Attempting to write config file: '{0}'", filePath);

            try
            {
                if (fileLock.TryEnter())
                    using (StreamWriter sw = new StreamWriter(filePath))
                    {
                        sw.Write(configData);
                        sw.Flush();
                    }
                else
                    Debug.Console(0, Debug.ErrorLogLevel.Error, "Unable to enter FileLock");
            }
            catch (Exception e)
            {
                Debug.Console(0, Debug.ErrorLogLevel.Error, "Error: Config write failed: \r{0}", e);
            }
            finally
            {
                if (fileLock != null && !fileLock.Disposed)
                    fileLock.Leave();
            }
        }
    }
}