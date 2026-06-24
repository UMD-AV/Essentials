using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Devices.Common.Microphones
{
    public class MicController
    {
        [JsonProperty("size")] public int Size { get; set; }
        [JsonProperty("mics")] public string[] MicKeys { get; set; }
    }

    public interface IWirelessMicReceiver
    {
        bool TryAssignMicrophone(string micKey, WirelessMic microphone);
        void ReleaseMicrophone(string micKey);
    }

    public static class WirelessMicAssignmentManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IWirelessMicReceiver> Receivers = new List<IWirelessMicReceiver>();

        private static readonly Dictionary<string, WirelessMic> PendingMicrophones =
            new Dictionary<string, WirelessMic>(StringComparer.OrdinalIgnoreCase);

        public static void RegisterReceiver(IWirelessMicReceiver receiver)
        {
            if (receiver == null) return;

            lock (SyncRoot)
            {
                if (!Receivers.Contains(receiver))
                    Receivers.Add(receiver);
            }

            RetryPendingAssignments();
        }

        public static void UnregisterReceiver(IWirelessMicReceiver receiver)
        {
            if (receiver == null) return;

            lock (SyncRoot)
            {
                Receivers.Remove(receiver);
            }
        }

        public static bool AssignFirstAvailable(string micKey, WirelessMic microphone)
        {
            if (string.IsNullOrEmpty(micKey) || microphone == null) return false;

            List<IWirelessMicReceiver> receivers;
            lock (SyncRoot)
            {
                PendingMicrophones[micKey] = microphone;
                receivers = new List<IWirelessMicReceiver>(Receivers);
            }

            foreach (IWirelessMicReceiver receiver in receivers)
            {
                if (!receiver.TryAssignMicrophone(micKey, microphone)) continue;

                lock (SyncRoot)
                {
                    PendingMicrophones.Remove(micKey);
                }

                return true;
            }

            microphone.MicrophonePresent = false;
            microphone.ErrorString = "No available wireless microphone receiver channel";
            return false;
        }

        public static void Release(string micKey)
        {
            if (string.IsNullOrEmpty(micKey)) return;

            List<IWirelessMicReceiver> receivers;
            lock (SyncRoot)
            {
                PendingMicrophones.Remove(micKey);
                receivers = new List<IWirelessMicReceiver>(Receivers);
            }

            foreach (IWirelessMicReceiver receiver in receivers)
                receiver.ReleaseMicrophone(micKey);
        }

        public static void RetryPendingAssignments()
        {
            List<KeyValuePair<string, WirelessMic>> pending;
            lock (SyncRoot)
            {
                pending = new List<KeyValuePair<string, WirelessMic>>(PendingMicrophones);
            }

            foreach (KeyValuePair<string, WirelessMic> item in pending)
                AssignFirstAvailable(item.Key, item.Value);
        }
    }

    public static class MicControllerUtilities
    {
        public static T[] BuildMicrophones<T>(EssentialsBridgeableDevice parent, int count, MicController config,
            string model, bool addConfiguredMicsToDeviceManager, Func<string, string, T> factory)
            where T : WirelessMic
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (factory == null) throw new ArgumentNullException("factory");
            if (count < 0) count = 0;

            config = config ?? new MicController();
            T[] microphones = new T[count];

            for (ushort i = 0; i < count; i++)
            {
                string configuredKey = GetConfiguredMicKey(config, i);
                string defaultKey = string.Format("{0}-mic{1}", parent.Key, i);
                string micKey = string.IsNullOrEmpty(configuredKey) ? defaultKey : configuredKey;
                T microphone = factory(micKey, defaultKey);

                microphone.MicrophoneEnabled = true;
                microphone.Model = model;
                microphones[i] = microphone;

                if (!addConfiguredMicsToDeviceManager || string.IsNullOrEmpty(configuredKey)) continue;

                try
                {
                    DeviceManager.AddDevice(microphone);
                }
                catch (Exception e)
                {
                    Debug.ConsoleWithLog(0, parent, "Exception adding mic '{0}' to device manager: {1}", micKey,
                        e.Message);
                }
            }

            return microphones;
        }

        public static void ArmFiveAmDockCheckTimer(CTimer timer)
        {
            if (timer == null) return;

            DateTime now = DateTime.Now;
            DateTime fiveAm = DateTime.Today.AddHours(5);
            if (now >= fiveAm)
                fiveAm = fiveAm.AddDays(1);

            double totalMilliseconds = (fiveAm - now).TotalMilliseconds + 10000;
            int timeout = totalMilliseconds > int.MaxValue ? int.MaxValue : (int)totalMilliseconds;
            timer.Reset(timeout);
        }

        public static void SetTransmitterStatus(WirelessMic microphone, string status)
        {
            if (microphone == null) return;

            microphone.State = status;
            microphone.OnDock = !string.IsNullOrEmpty(status) && status.Equals("ON_CHARGER");
            microphone.MicrophonePresent = IsTransmitterPresent(status);
        }

        public static bool IsTransmitterPresent(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;

            switch (status.ToUpper())
            {
                case "UNKNOWN":
                case "MISSING":
                case "NO_TX":
                case "NOT_PRESENT":
                    return false;
                default:
                    return true;
            }
        }

        public static int GetConfiguredSize(MicController config, int defaultSize, int maxSize)
        {
            if (config == null || config.Size <= 0)
                return defaultSize;

            if (config.Size > maxSize)
                return maxSize;

            return config.Size;
        }

        public static string GetConfiguredMicKey(MicController config, int index)
        {
            if (config == null || config.MicKeys == null || index < 0 || index >= config.MicKeys.Length)
                return null;

            return config.MicKeys[index];
        }

        public static bool MicKeyAllowed(MicController config, string micKey)
        {
            if (string.IsNullOrEmpty(micKey) || config == null || config.MicKeys == null ||
                config.MicKeys.Length == 0)
                return true;

            foreach (string configuredKey in config.MicKeys)
            {
                if (string.IsNullOrEmpty(configuredKey)) continue;
                if (configuredKey.Equals(micKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

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
        /// <see cref="PepperDash.Essentials.Core.MonitorStatus" />
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