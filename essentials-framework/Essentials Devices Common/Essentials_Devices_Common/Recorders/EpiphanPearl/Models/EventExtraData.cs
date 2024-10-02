using Newtonsoft.Json;

namespace PepperDash.Essentials.EpiphanPearl.Models
{
    public class EventExtraData
    {
        [JsonProperty("SessionId")] public string SessionId { get; set; }

        [JsonProperty("folderId")] public string FolderId { get; set; }

        [JsonProperty("folderName")] public string FolderName { get; set; }
    }
}