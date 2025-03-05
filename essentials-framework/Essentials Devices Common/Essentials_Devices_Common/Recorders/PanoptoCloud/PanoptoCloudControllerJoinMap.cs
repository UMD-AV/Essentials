using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.PanoptoCloud
{
    public class PanoptoCloudControllerJoinMap : JoinMapBaseAdvanced
    {
        public PanoptoCloudControllerJoinMap(uint joinStart) : base(joinStart, typeof(PanoptoCloudControllerJoinMap))
        {
        }

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

        [JoinName("IsPaused")] public JoinDataComplete IsPaused = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
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
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Recording Is In Progress"
            });

        [JoinName("RecorderStateVal")] public JoinDataComplete RecorderStateVal = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog,
                Description = "Recorder state string"
            });

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
                Description = "Device name"
            });

        [JoinName("StartRecordingStatus")] public JoinDataComplete StartRecordingStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Status and error messages of recording while starting"
            });

        [JoinName("RecorderState")] public JoinDataComplete RecorderState = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 20,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Recorder state string"
            });
    }
}