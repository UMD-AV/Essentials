using System;

namespace PepperDash.Essentials.Core.Bridges
{
    public class UiJoinMap : JoinMapBaseAdvanced
    {
        //Serial

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "UI name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("DefaultRoomKey")] public JoinDataComplete DefaultRoomKey = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Default room key",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("UserPassword")] public JoinDataComplete UserPassword = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Numeric password to use UI (blank to disable)",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("TechPassword")] public JoinDataComplete TechPassword = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Numeric password to enter tech pages",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("ScheduleKey")] public JoinDataComplete ScheduleKey = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Key to scheduler for TSS panels",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        //Recorder Joins
        [JoinName("RecorderStartAdHoc")] public JoinDataComplete RecorderStartAdHoc = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Start ad hoc recording"
            });

        [JoinName("RecorderResetAdHoc")] public JoinDataComplete RecorderResetAdHoc = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Reset ad hoc recording data and settings"
            });

        [JoinName("RecorderSubpageActive")] public JoinDataComplete RecorderSubpageActive = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Recorder subpage active on UI"
            });

        [JoinName("RecorderCancelAdHoc")] public JoinDataComplete RecorderCancelAdHoc = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Cancel ad hoc recording"
            });

        [JoinName("RecorderStartFailed")] public JoinDataComplete RecorderStartFailed = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Ad hoc recording start failed"
            });

        [JoinName("RecorderSelectCurrentUser")]
        public JoinDataComplete RecorderSelectCurrentUser = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Select current user from search results"
            });

        [JoinName("RecorderSelectEndTime")] public JoinDataComplete RecorderSelectEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 25
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Select recording end time and feedback"
            });

        [JoinName("SetRecorderKey")] public JoinDataComplete SetRecorderKey = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Set the recorder key for ui control",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("RecorderCurrentUser")] public JoinDataComplete RecorderCurrentUser = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Current user name for recording folder search"
            });

        [JoinName("RecorderCurrentFolder")] public JoinDataComplete RecorderCurrentFolder = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Current folder name for recording"
            });

        [JoinName("SetNextRecordingStartTime")]
        public JoinDataComplete SetNextRecordingStartTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Set the next recording start time"
            });

        [JoinName("SetRecordingName")] public JoinDataComplete SetRecordingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Set/get recording name"
            });

        [JoinName("SetRecordingMeetingEndTime")]
        public JoinDataComplete SetRecordingMeetingEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 10,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Set current meeting end time for recording purposes"
            });

        [JoinName("StartRecordingStatus")] public JoinDataComplete StartRecordingStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 10,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Start recording status"
            });

        [JoinName("RecorderUserSearchResults")]
        public JoinDataComplete RecorderUserSearchResults = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 8
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "User search results"
            });

        [JoinName("RecorderEndTime")] public JoinDataComplete RecorderEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 20,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recording end time selected"
            });

        [JoinName("RecorderEndTimeResults")] public JoinDataComplete RecorderEndTimeResults = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 25
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recording end time results"
            });


        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public UiJoinMap(uint joinStart)
            : this(joinStart, typeof(UiJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected UiJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}