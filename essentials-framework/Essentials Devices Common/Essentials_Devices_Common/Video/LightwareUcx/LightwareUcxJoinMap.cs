using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.LightwareUcx
{
    public class LightwareUcxJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Switcher Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData { JoinNumber = 11, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Switcher Online", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("UsbAutoRouteToggle")] public JoinDataComplete UsbAutoRouteToggle = new JoinDataComplete(
            new JoinData { JoinNumber = 21, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "USB Auto Route set/get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("VideoSyncStatus")] public JoinDataComplete VideoSyncStatus = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Input Video Sync", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OutputVideoConnected")] public JoinDataComplete OutputVideoConnected = new JoinDataComplete(
            new JoinData { JoinNumber = 200, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Output Video Connected", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OutputMonitoringEnabled")] public JoinDataComplete OutputMonitoringEnabled = new JoinDataComplete(
            new JoinData { JoinNumber = 300, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Enabled monitoring of video output connection",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("UsbHostAvailable")] public JoinDataComplete UsbHostAvailable = new JoinDataComplete(
            new JoinData { JoinNumber = 500, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Usb host is available/connected",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OutputVideo")] public JoinDataComplete OutputVideo = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Output Video Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("OutputAudio")] public JoinDataComplete OutputAudio = new JoinDataComplete(
            new JoinData { JoinNumber = 300, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Output Audio Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("OutputUsb")] public JoinDataComplete OutputUsb = new JoinDataComplete(
            new JoinData { JoinNumber = 500, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Output USB Set / Get", JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("InputNames")] public JoinDataComplete InputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 100, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Input Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("OutputNames")] public JoinDataComplete OutputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 300, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Output Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("InputVideoNames")] public JoinDataComplete InputVideoNames =
            new JoinDataComplete(new JoinData { JoinNumber = 500, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Switcher Video Input Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("InputAudioNames")] public JoinDataComplete InputAudioNames =
            new JoinDataComplete(new JoinData { JoinNumber = 700, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Switcher Audio Input Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("OutputVideoNames")] public JoinDataComplete OutputVideoNames =
            new JoinDataComplete(new JoinData { JoinNumber = 900, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Switcher Video Output Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("OutputAudioNames")] public JoinDataComplete OutputAudioNames =
            new JoinDataComplete(new JoinData { JoinNumber = 1100, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Switcher Audio Output Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        [JoinName("OutputCurrentVideoInputNames")]
        public JoinDataComplete OutputCurrentVideoInputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 1200, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Video Output Currently Routed Video Input Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial
            });

        [JoinName("OutputCurrentAudioInputNames")]
        public JoinDataComplete OutputCurrentAudioInputNames = new JoinDataComplete(
            new JoinData { JoinNumber = 1600, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Audio Output Currently Routed Audio Input Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial
            });

        [JoinName("InputCurrentResolution")] public JoinDataComplete InputCurrentResolution = new JoinDataComplete(
            new JoinData { JoinNumber = 1400, JoinSpan = 10 },
            new JoinMetadata
            {
                Description = "Switcher Input Current Resolution", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("InputUsbNames")] public JoinDataComplete InputUsbNames =
            new JoinDataComplete(new JoinData { JoinNumber = 1800, JoinSpan = 10 },
                new JoinMetadata
                {
                    Description = "Switcher Usb Input Names",
                    JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                    JoinType = eJoinType.Serial
                });

        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public LightwareUcxJoinMap(uint joinStart)
            : this(joinStart, typeof(LightwareUcxJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected LightwareUcxJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}