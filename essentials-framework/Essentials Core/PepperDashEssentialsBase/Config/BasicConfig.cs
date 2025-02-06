using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PepperDash.Essentials.Core.Config
{
    /// <summary>
    ///  Override this and splice on specific room type behavior, as well as other properties
    /// </summary>
    public class BasicConfig
    {
        [JsonProperty("info")] public InfoConfig Info { get; set; }

        [JsonProperty("devices")] public List<DeviceConfig> Devices { get; set; }

        [JsonProperty("joinMaps")] public Dictionary<string, JObject> JoinMaps { get; set; }

        protected BasicConfig()
        {
            Info = new InfoConfig();
            Devices = new List<DeviceConfig>();
            JoinMaps = new Dictionary<string, JObject>();
        }

        /// <summary>
        /// Checks devices for an item with a key that matches and returns it if found. Otherwise, returns null
        /// </summary>
        /// <param name="key">Key of a desired device</param>
        /// <returns></returns>
        public DeviceConfig GetDeviceForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            DeviceConfig deviceConfig = Devices.FirstOrDefault(d => d.Key.Equals(key));

            if (deviceConfig != null)
                return deviceConfig;
            return null;
        }
    }
}