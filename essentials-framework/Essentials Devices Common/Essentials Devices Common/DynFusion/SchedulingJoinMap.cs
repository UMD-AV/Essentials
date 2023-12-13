using PepperDash.Essentials.Core;

namespace DynFusion
{
	public class SchedulingJoinMap : JoinMapBaseAdvanced
	{
		[JoinName("GetSchedule")]
		public JoinDataComplete GetSchedule = new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 }, new JoinMetadata { Label = "GetSchedule", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });
		[JoinName("ScheduleOnline")]
		public JoinDataComplete ScheduleOnline = new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 }, new JoinMetadata { Label = "ScheduleOnline", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });
		[JoinName("GetRoomInfo")]
		public JoinDataComplete GetRoomInfo = new JoinDataComplete(new JoinData { JoinNumber = 4, JoinSpan = 1 }, new JoinMetadata { Label = "GetRoomInfo", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Digital });
        [JoinName("PushNotificationRegistered")]
		public JoinDataComplete PushNotificationRegistered = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 }, new JoinMetadata { Label = "PushNotificationRegistered", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });
		[JoinName("RoomID")]
		public JoinDataComplete RoomID = new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 }, new JoinMetadata { Label = "RoomID", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("RoomLocation")]
		public JoinDataComplete RoomLocation = new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 }, new JoinMetadata { Label = "RoomLocation", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingOrganizer")]
		public JoinDataComplete CurrentMeetingOrganizer = new JoinDataComplete(new JoinData { JoinNumber = 21, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingOrganizer", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingSubject")]
		public JoinDataComplete CurrentMeetingSubject = new JoinDataComplete(new JoinData { JoinNumber = 22, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingSubject", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("CurrentMeetingInProgress")]
        public JoinDataComplete CurrentMeetingInProgress = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 }, new JoinMetadata { Label = "CurentMeetingInProgress", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });
        [JoinName("CurrentMeetingMeetingID")]
		public JoinDataComplete CurrentMeetingMeetingID = new JoinDataComplete(new JoinData { JoinNumber = 23, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingMeetingID", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingStartTime")]
		public JoinDataComplete CurrentMeetingStartTime = new JoinDataComplete(new JoinData { JoinNumber = 24, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingStartTime", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingStartDate")]
		public JoinDataComplete CurrentMeetingStartDate = new JoinDataComplete(new JoinData { JoinNumber = 25, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingStartDate", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingEndTime")]
		public JoinDataComplete CurrentMeetingEndTime = new JoinDataComplete(new JoinData { JoinNumber = 26, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingEndTime", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingEndDate")]
		public JoinDataComplete CurrentMeetingEndDate = new JoinDataComplete(new JoinData { JoinNumber = 27, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingEndDate", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("CurrentMeetingDuration")]
		public JoinDataComplete CurrentMeetingDuration = new JoinDataComplete(new JoinData { JoinNumber = 28, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingDuration", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("CurrentMeetingOrganizerSMTP")]
        public JoinDataComplete CurrentMeetingOrganizerSMTP = new JoinDataComplete(new JoinData { JoinNumber = 30, JoinSpan = 1 }, new JoinMetadata { Label = "CurrentMeetingOrganizerSMTP", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("NextMeetingOrganizer")]
		public JoinDataComplete NextMeetingOrganizer = new JoinDataComplete(new JoinData { JoinNumber = 31, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingOrganizer", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingSubject")]
		public JoinDataComplete NextMeetingSubject = new JoinDataComplete(new JoinData { JoinNumber = 32, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingSubject", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingMeetingID")]
		public JoinDataComplete NextMeetingMeetingID = new JoinDataComplete(new JoinData { JoinNumber = 33, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingMeetingID", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingStartTime")]
		public JoinDataComplete NextMeetingStartTime = new JoinDataComplete(new JoinData { JoinNumber = 34, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingStartTime", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingStartDate")]
		public JoinDataComplete NextMeetingStartDate = new JoinDataComplete(new JoinData { JoinNumber = 35, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingStartDate", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingEndTime")]
		public JoinDataComplete NextMeetingEndTime = new JoinDataComplete(new JoinData { JoinNumber = 36, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingEndTime", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingEndDate")]
		public JoinDataComplete NextMeetingEndDate = new JoinDataComplete(new JoinData { JoinNumber = 37, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingEndDate", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
		[JoinName("NextMeetingDuration")]
		public JoinDataComplete NextMeetingDuration = new JoinDataComplete(new JoinData { JoinNumber = 38, JoinSpan = 1 }, new JoinMetadata { Label = "NextMeetingDuration", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        //Arrays of 20 for meeting list
        [JoinName("MeetingSubject")]
		public JoinDataComplete MeetingSubject = new JoinDataComplete(new JoinData { JoinNumber = 41, JoinSpan = 20 }, new JoinMetadata { Label = "MeetingSubject", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("MeetingOrganizer")]
        public JoinDataComplete MeetingOrganizer = new JoinDataComplete(new JoinData { JoinNumber = 61, JoinSpan = 20 }, new JoinMetadata { Label = "MeetingSubject", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("MeetingTime")]
        public JoinDataComplete MeetingTime = new JoinDataComplete(new JoinData { JoinNumber = 81, JoinSpan = 20 }, new JoinMetadata { Label = "MeetingSubject", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });
        [JoinName("MeetingInProgress")]
        public JoinDataComplete MeetingInProgress = new JoinDataComplete(new JoinData { JoinNumber = 41, JoinSpan = 20 }, new JoinMetadata { Label = "MeetingInProgress", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });
		
		public SchedulingJoinMap(uint joinStart)
			: this(joinStart, typeof(SchedulingJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
		protected SchedulingJoinMap(uint joinStart, System.Type type)
			: base(joinStart, type)
        {
        }

	}
}