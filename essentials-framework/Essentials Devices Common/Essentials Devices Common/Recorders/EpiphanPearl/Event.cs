using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PepperDash.Essentials.EpiphanPearl.Models
{
    public class Event
    {
        [JsonProperty("start")]
        [JsonConverter(typeof(SecondEpochConverter))]
        public DateTime Start { get; set; }

        [JsonProperty("finish")]
        [JsonConverter(typeof(SecondEpochConverter))]
        public DateTime Finish { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("extra_data")]
        public EventExtraData ExtraData { get; set; }
    }

    public class SecondEpochConverter : DateTimeConverterBase
    {
        private static DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue(((DateTime)value - _epoch).TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            try
            {
                long seconds = long.Parse(reader.Value.ToString());
                return _epoch.AddSeconds(seconds);
            }
            catch (FormatException)
            {
                // Handle the case when the value is not a valid long
                // You can choose to return a default value, throw a custom exception, or log an error
                // For example:
                return _epoch;
            }
            catch (OverflowException)
            {
                // Handle the case when the value is too large to fit into a long
                // You can choose to return a default value, throw a custom exception, or log an error
                // For example:
                return DateTime.MaxValue;
            }
        }
    }
}