using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace QscQsysDspPlugin
{
    /// <summary>
    /// QSC DSP Camera control join map
    /// </summary>
    public class QscDspCameraDeviceJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("Up")] public JoinDataComplete Up =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Up",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Down")] public JoinDataComplete Down =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Down",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Left")] public JoinDataComplete Left =
            new JoinDataComplete(new JoinData { JoinNumber = 3, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Left",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Right")] public JoinDataComplete Right =
            new JoinDataComplete(new JoinData { JoinNumber = 4, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Right",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ZoomIn")] public JoinDataComplete ZoomIn =
            new JoinDataComplete(new JoinData { JoinNumber = 5, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Zoom In",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("ZoomOut")] public JoinDataComplete ZoomOut =
            new JoinDataComplete(new JoinData { JoinNumber = 6, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Zoom Out",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("Online")] public JoinDataComplete Online =
            new JoinDataComplete(new JoinData { JoinNumber = 9, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Online Feedback",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PresetRecall")] public JoinDataComplete PresetRecallStart =
            new JoinDataComplete(new JoinData { JoinNumber = 10, JoinSpan = 20 },
                new JoinMetadata
                {
                    Description = "Camera Preset Recall",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PresetStore")] public JoinDataComplete PresetStoreStart =
            new JoinDataComplete(new JoinData { JoinNumber = 30, JoinSpan = 20 },
                new JoinMetadata
                {
                    Description = "Camera Preset Store",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PrivacyOn")] public JoinDataComplete PrivacyOn =
            new JoinDataComplete(new JoinData { JoinNumber = 48, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Privacy On",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PrivacyOff")] public JoinDataComplete PrivacyOff =
            new JoinDataComplete(new JoinData { JoinNumber = 49, JoinSpan = 1 },
                new JoinMetadata
                {
                    Description = "Camera Privacy Off",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Digital
                });

        [JoinName("PresetNames")] public JoinDataComplete PresetNamesStart =
            new JoinDataComplete(new JoinData { JoinNumber = 2, JoinSpan = 20 },
                new JoinMetadata
                {
                    Description = "Camera Preset Names",
                    JoinCapabilities = eJoinCapabilities.FromSIMPL,
                    JoinType = eJoinType.Serial
                });

        public QscDspCameraDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(QscDspCameraDeviceJoinMap))
        {
        }
    }
}