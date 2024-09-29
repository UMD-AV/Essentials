using System;

namespace PepperDash.Essentials.Core.Fusion
{
    public class ScheduleChangeEventArgs : EventArgs
    {
        public RoomSchedule Schedule { get; set; }
    }

    public class MeetingChangeEventArgs : EventArgs
    {
        public Event Meeting { get; set; }
    }
}