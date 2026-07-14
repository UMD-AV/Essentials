using Newtonsoft.Json;

namespace UmdEssentials.EpiphanPearl.Models
{
    public class BaseResponse<T>
    {
        [JsonProperty("status")] public string Status { get; set; }

        [JsonProperty("message")] public string Message { get; set; }

        [JsonProperty("result")] public T Result { get; set; }
    }
}