using Newtonsoft.Json;

namespace PepperDash_Essentials_Core.PartitionSensor
{
    public class GlsPartitionSensorPropertiesConfig
    {
        /// <summary>
        /// Sets the sensor sensitivity        
        /// </summary>
        /// <remarks>
        /// The sensitivity range shall be between 1(lowest) to 10 (highest).
        /// </remarks>
        [JsonProperty("sensitivity")]
        public ushort? Sensitivity { get; set; }
    }
}