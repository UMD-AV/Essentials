using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;

namespace PepperDash.Essentials.PanoptoCloud
{
    public static class Utils
    {
        public static HttpsClient WithDefaultSettings(this HttpsClient client)
        {
            client.IncludeHeaders = false;
            client.KeepAlive = false;
            client.HostVerification = false;
            client.PeerVerification = false;

            return client;
        }

        public static string CurrentRecordingLength(this Guid currentRecordingId, DateTime startTime, DateTime endTime)
        {
            return currentRecordingId == Guid.Empty
                ? String.Empty
                : String.Format("{0}", (endTime - startTime).TotalMinutes);
        }

        public static string CurrentRecordingTimeRemaining(this Guid currentRecordingId, DateTime endTime)
        {
            return currentRecordingId == Guid.Empty
                ? String.Empty
                : String.Format("{0}", Math.Round((endTime - DateTime.Now).TotalMinutes));
        }

        public static bool TryGetValueFromSecureStorage(string key, out string value)
        {
            value = String.Empty;

            byte[] bytes;
            eCrestronSecureStorageStatus storageResult = CrestronSecureStorage.Retrieve(key,
                false,
                Encoding.ASCII.GetBytes(key),
                out bytes);

            if (storageResult != eCrestronSecureStorageStatus.Ok)
                return false;

            value = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            return true;
        }
    }
}