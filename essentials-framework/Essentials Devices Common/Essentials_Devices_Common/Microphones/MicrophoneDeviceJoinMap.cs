using System;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.Microphones
{
    public class MicrophoneDeviceJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")] public JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device online feedback",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        ///     Get/Set device audio mute on
        /// </summary>
        [JoinName("DeviceAudioMuteOn")] public JoinDataComplete DeviceAudioMuteOn = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Audio Mute On",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        ///     Get/Set device audio mute off
        /// </summary>
        [JoinName("DeviceAudioMuteOff")] public JoinDataComplete DeviceAudioMuteOff = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Audio Mute Off",
                JoinCapabilities = eJoinCapabilities.ToFromSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("IsWireless")] public JoinDataComplete IsWireless = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 4,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device is a wireless mic with a battery",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("OnDock")] public JoinDataComplete OnDock = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device is a wireless mic and on the charging dock",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("IsPresent")] public JoinDataComplete IsPresent = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 6,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device is a wireless mic and has link",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        [JoinName("PercentCharge")] public JoinDataComplete PercentCharge = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Battery percent charge", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("PercentHealth")] public JoinDataComplete PercentHealth = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Battery percent health", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("TempF")] public JoinDataComplete TempF = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Battery temp in F", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("BatteryErrorAnalog")] public JoinDataComplete BatteryErrorAnalog = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Battery error analog", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("BatteryRunTime")] public JoinDataComplete BatteryRunTime = new JoinDataComplete(
            new JoinData { JoinNumber = 5, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Battery run time in minutes", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("LinkState")] public JoinDataComplete LinkState = new JoinDataComplete(
            new JoinData { JoinNumber = 6, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Wireless microphone link state",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        [JoinName("Name")] public JoinDataComplete Name = new JoinDataComplete(
            new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Microphone Name", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("ErrorString")] public JoinDataComplete ErrorString = new JoinDataComplete(
            new JoinData { JoinNumber = 2, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Error String", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("State")] public JoinDataComplete State = new JoinDataComplete(
            new JoinData { JoinNumber = 3, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "State string", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        [JoinName("Model")] public JoinDataComplete Model = new JoinDataComplete(
            new JoinData { JoinNumber = 4, JoinSpan = 1 },
            new JoinMetadata
            {
                Description = "Model name string", JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        ///     Get device firmware version
        /// </summary>
        [JoinName("DeviceFirmwareVersion")] public JoinDataComplete DeviceFirmwareVersion = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 5,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Firmware Version",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        ///     Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public MicrophoneDeviceJoinMap(uint joinStart)
            : this(joinStart, typeof(MicrophoneDeviceJoinMap))
        {
        }

        /// <summary>
        ///     Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected MicrophoneDeviceJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}