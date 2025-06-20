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

        [JoinName("HdmiOutputSource")] public JoinDataComplete HdmiOutputSource = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "HdmiOutputSource Set/Get"
            });

        [JoinName("Channel1Layout")] public JoinDataComplete Channel1Layout = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Channel1Layout Set/Get"
            });

        [JoinName("Channel2Layout")] public JoinDataComplete Channel2Layout = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Channel2Layout Set/Get"
            });

        [JoinName("Channel3Layout")] public JoinDataComplete Channel3Layout = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Channel3Layout Set/Get"
            });

        [JoinName("PanoptoKey")] public JoinDataComplete PanoptoKey = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Panopto Cloud Device Key"
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

        [JoinName("Extend5")] public JoinDataComplete Extend5 = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Extend Recording 5 minutes"
            });

        [JoinName("Extend15")] public JoinDataComplete Extend15 = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Extend Recording 15 minutes"
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

        [JoinName("Extend5Enable")] public JoinDataComplete Extend5Enable = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 8,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Extend 5 minutes enabled"
            });

        [JoinName("Extend15Enable")] public JoinDataComplete Extend15Enable = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 9,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Extend 15 minutes enabled"
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

        [JoinName("NextRecordingIn5m")] public JoinDataComplete NextRecordingIn5m = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 21,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Next recording is starting in 5m"
            });

        [JoinName("NextRecordingIn10m")] public JoinDataComplete NextRecordingIn10m = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 22,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Next recording is starting in 10m"
            });

        [JoinName("Steam1Url")] public JoinDataComplete Stream1Url = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 31,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Stream 1 URL"
            });

        [JoinName("Steam2Url")] public JoinDataComplete Stream2Url = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 32,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Stream 2 URL"
            });

        [JoinName("Steam3Url")] public JoinDataComplete Stream3Url = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 33,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Stream 3 URL"
            });
    }
}