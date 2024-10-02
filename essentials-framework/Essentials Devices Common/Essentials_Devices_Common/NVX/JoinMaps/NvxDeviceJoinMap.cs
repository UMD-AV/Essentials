using PepperDash.Essentials.Core;

namespace NvxEpi.JoinMaps
{
    public class NvxDeviceJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("AudioInput")] public JoinDataComplete AudioInput = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog,
                Description = "Audio Input Source"
            });

        [JoinName("AudioInputString")] public JoinDataComplete AudioInputString = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 7,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Audio Input Source"
            });

        [JoinName("DeviceName")] public JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1,
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial,
                Description = "Device Name"
            });

        [JoinName("DeviceOnline")] public JoinDataComplete DeviceOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Device Online"
            });

        [JoinName("Hdmi1SyncDetected")] public JoinDataComplete Hdmi1SyncDetected = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Hdmi1 Sync Detected"
            });

        [JoinName("Hdmi2SyncDetected")] public JoinDataComplete Hdmi2SyncDetected = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital,
                Description = "Hdmi2 Sync Detected"
            });

        [JoinName("VideoInput")] public JoinDataComplete VideoInput = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.AnalogSerial,
                Description = "Video Input Source"
            });

        public NvxDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(NvxDeviceJoinMap))
        {
        }
    }
}