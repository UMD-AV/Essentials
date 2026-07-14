using System;
using Newtonsoft.Json;
using PepperDash.Core;
using UmdEssentials.Core;

namespace UmdEssentials.Devices.Common.Microphones
{
    public class MicController
    {
        [JsonProperty("mics")] public string[] MicKeys { get; set; }
    }

    public static class MicControllerUtilities
    {
        public static void FireMicrophoneFeedbacks(WirelessMic[] microphones)
        {
            if (microphones == null) return;

            foreach (WirelessMic microphone in microphones)
                if (microphone != null)
                    microphone.FireUpdate();
        }
    }

    public class MicControllerJoinMap : JoinMapBaseAdvanced
    {
        /// <summary>
        ///     Plugin device BridgeJoinMap constructor
        /// </summary>
        /// <param name="joinStart">This will be the join it starts on the EISC bridge</param>
        public MicControllerJoinMap(uint joinStart)
            : base(joinStart, typeof(MicControllerJoinMap))
        {
        }

        #region Digital

        /// <summary>
        ///     Get device online feedback
        /// </summary>
        [JoinName("IsOnline")] public readonly JoinDataComplete IsOnline = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Is Online",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Digital
            });

        /// <summary>
        /// Refresh all microphone controller data.
        /// </summary>
        [JoinName("RefreshData")] public readonly JoinDataComplete RefreshData = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 3,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Refresh all microphone controller data",
                JoinCapabilities = eJoinCapabilities.FromSIMPL,
                JoinType = eJoinType.Digital
            });

        #endregion

        #region Analog

        /// <summary>
        ///     Get device socket status join map
        /// </summary>
        /// <see cref="Crestron.SimplSharp.CrestronSockets.SocketStatus" />
        [JoinName("SocketStatus")] public readonly JoinDataComplete SocketStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Socket SocketStatus",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        /// <summary>
        ///     Get device monitor status join map
        /// </summary>
        /// <see cref="Core.MonitorStatus" />
        [JoinName("MonitorStatus")] public JoinDataComplete MonitorStatus = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Monitor Status",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Analog
            });

        #endregion

        #region Serial

        /// <summary>
        ///     Get device name
        /// </summary>
        [JoinName("DeviceName")] public readonly JoinDataComplete DeviceName = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 1,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Name",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        ///     Get the device model
        /// </summary>
        [JoinName("DeviceModel")] public readonly JoinDataComplete DeviceModel = new JoinDataComplete(
            new JoinData
            {
                JoinNumber = 2,
                JoinSpan = 1
            },
            new JoinMetadata
            {
                Description = "Device Model",
                JoinCapabilities = eJoinCapabilities.ToSIMPL,
                JoinType = eJoinType.Serial
            });

        /// <summary>
        ///     Get the device firmware version
        /// </summary>
        [JoinName("DeviceFirmwareVersion")] public readonly JoinDataComplete DeviceFirmwareVersion =
            new JoinDataComplete(
                new JoinData
                {
                    JoinNumber = 4,
                    JoinSpan = 1
                },
                new JoinMetadata
                {
                    Description = "Device Firmware Version",
                    JoinCapabilities = eJoinCapabilities.ToSIMPL,
                    JoinType = eJoinType.Serial
                });

        #endregion
    }
}