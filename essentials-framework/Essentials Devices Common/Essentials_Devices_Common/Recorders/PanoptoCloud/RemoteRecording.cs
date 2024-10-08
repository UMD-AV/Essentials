using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

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
}