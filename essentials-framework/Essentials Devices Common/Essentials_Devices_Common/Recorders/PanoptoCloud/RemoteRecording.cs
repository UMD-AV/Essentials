using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class RecorderScheduleEntry
    {
        public Guid RecorderId { get; set; }
        public bool SuppressPrimaryCapture { get; set; }
        public bool SuppressSecondaryCapture { get; set; }
        public string RecorderDescription { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class ScheduledRecording
    {
        public List<RecorderScheduleEntry> RecorderScheduleEntries { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }

        public ScheduledRecording()
        {
            RecorderScheduleEntries = new List<RecorderScheduleEntry>();
        }
    }

    public class ErrorResponse
    {
        [JsonProperty("Error")] public ErrorDetail Error { get; set; }
    }

    public class ErrorDetail
    {
        [JsonProperty("ErrorCode")] public string ErrorCode { get; set; }

        [JsonProperty("Message")] public string Message { get; set; }

        [JsonProperty("Success")] public bool Success { get; set; }

        [JsonProperty("ErrorSource")] public object ErrorSource { get; set; }
    }
}