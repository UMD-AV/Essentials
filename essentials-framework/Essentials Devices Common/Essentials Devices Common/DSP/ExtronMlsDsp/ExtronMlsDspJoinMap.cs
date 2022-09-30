using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace ExtronMlsDsp
{
	/// <summary>
	/// Extron MLS DSP Join Map
	/// </summary>
	public class ExtronMlsDspDeviceJoinMap : JoinMapBaseAdvanced
	{
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Online Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("Name")]
        public JoinDataComplete Name =
            new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("EnableLevelSend")]
        public JoinDataComplete EnableLevelSend =
            new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Enable Level Sending from SIMPL",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVisible")]
        public JoinDataComplete ChannelVisible =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Visible Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteToggle")]
        public JoinDataComplete ChannelMuteToggle =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute Toggle Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOn")]
        public JoinDataComplete ChannelMuteOn =
            new JoinDataComplete(new JoinData { JoinNumber = 600, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute On",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelMuteOff")]
        public JoinDataComplete ChannelMuteOff =
            new JoinDataComplete(new JoinData { JoinNumber = 800, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Mute Off",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeUp")]
        public JoinDataComplete ChannelVolumeUp =
            new JoinDataComplete(new JoinData { JoinNumber = 1000, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Up",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolumeDown")]
        public JoinDataComplete ChannelVolumeDown =
            new JoinDataComplete(new JoinData { JoinNumber = 1200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Down",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("ChannelVolume")]
        public JoinDataComplete ChannelVolume =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Volume Set/Get",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.DigitalAnalog
            });

        [JoinName("ChannelType")]
        public JoinDataComplete ChannelType =
            new JoinDataComplete(new JoinData { JoinNumber = 400, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Type Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("ChannelName")]
        public JoinDataComplete ChannelName =
            new JoinDataComplete(new JoinData { JoinNumber = 200, JoinSpan = 200 },
            new JoinMetadata
            {
                Description = "Channel Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Presets")]
        public JoinDataComplete Presets =
            new JoinDataComplete(new JoinData { JoinNumber = 100, JoinSpan = 100 },
            new JoinMetadata
            {
                Description = "Preset Recall with Name Feedback",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.DigitalSerial
            });

        public ExtronMlsDspDeviceJoinMap(uint joinStart)
            : base(joinStart, typeof(ExtronMlsDspDeviceJoinMap))
        {
        }
	}
}