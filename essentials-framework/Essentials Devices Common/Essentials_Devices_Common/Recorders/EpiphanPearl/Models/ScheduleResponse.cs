using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl.Models
{
    public class ScheduleResponse<T>
    {
        [JsonProperty("status")] public string Status { get; set; }

        [JsonProperty("result")] public T Result { get; set; }

        [JsonProperty("message")] public string Message { get; set; }
    }
}