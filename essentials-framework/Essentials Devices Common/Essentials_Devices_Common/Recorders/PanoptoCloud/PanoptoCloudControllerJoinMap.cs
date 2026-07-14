using UmdEssentials.Core;

namespace UmdEssentials.PanoptoCloud
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
    }
}