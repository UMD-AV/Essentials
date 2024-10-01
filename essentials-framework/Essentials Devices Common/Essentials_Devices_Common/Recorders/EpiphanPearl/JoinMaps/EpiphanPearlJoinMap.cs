using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.EpiphanPearl.JoinMaps
{
    public class EpiphanPearlJoinMap : JoinMapBaseAdvanced
    {
        public EpiphanPearlJoinMap(uint joinStart) : base(joinStart, typeof(EpiphanPearlJoinMap))
        {
        }

        public EpiphanPearlJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recorder Name"
            });

        [JoinName("SetHostname")] public JoinDataComplete SetHostname = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Set Hostname"
            });

        [JoinName("RecorderOnline")] public JoinDataComplete RecorderOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Recorder Online"
            });

        [JoinName("Start")] public JoinDataComplete Start = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Start Recording"
            });

        [JoinName("Stop")] public JoinDataComplete Stop = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Stop Recording"
            });

        [JoinName("Pause")] public JoinDataComplete Pause = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Pause Recording"
            });

        [JoinName("Resume")] public JoinDataComplete Resume = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Resume Recording"
            });

        [JoinName("Extend")] public JoinDataComplete Extend = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Extend Recording"
            });

        [JoinName("CurrentRecordingId")] public JoinDataComplete CurrentRecordingId = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 11,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "CurrentRecordingId"
            });

        [JoinName("CurrentRecordingName")] public JoinDataComplete CurrentRecordingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 12,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recorder Name"
            });

        [JoinName("CurrentRecordingStartTime")]
        public JoinDataComplete CurrentRecordingStartTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 13,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "CurrentRecordingStartTime"
            });

        [JoinName("CurrentRecordingEndTime")] public JoinDataComplete CurrentRecordingEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 14,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "CurrentRecordingEndTime"
            });

        [JoinName("CurrentRecordingLength")] public JoinDataComplete CurrentRecordingLength = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 15,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "CurrentRecordingLength"
            });

        [JoinName("CurrentRecordingTimeRemaining")]
        public JoinDataComplete CurrentRecordingTimeRemaining = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 16,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "CurrentRecordingTimeRemaining"
            });

        [JoinName("NextRecordingId")] public JoinDataComplete NextRecordingId = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "NextRecordingId"
            });

        [JoinName("NextRecordingName")] public JoinDataComplete NextRecordingName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 22,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recorder Name"
            });

        [JoinName("NextRecordingStartTime")] public JoinDataComplete NextRecordingStartTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 23,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "NextRecordingStartTime"
            });

        [JoinName("NextRecordingEndTime")] public JoinDataComplete NextRecordingEndTime = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 24,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "NextRecordingEndTime"
            });

        [JoinName("NextRecordingLength")] public JoinDataComplete NextRecordingLength = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 25,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "NextRecordingLength"
            });

        [JoinName("IsPaused")] public JoinDataComplete IsPaused = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Recording Is Paused"
            });

        [JoinName("IsRecording")] public JoinDataComplete IsRecording = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Recording Is In Progress"
            });

        [JoinName("NextRecordingExists")] public JoinDataComplete NextRecordingExists = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 20,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Next Recording Exists"
            });
    }
}