using System;
using System.Collections.Generic;
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
                ? string.Empty
                : string.Format("{0}", (endTime - startTime).TotalMinutes);
        }

        public static string CurrentRecordingTimeRemaining(this Guid currentRecordingId, DateTime endTime)
        {
            return currentRecordingId == Guid.Empty
                ? string.Empty
                : string.Format("{0}", Math.Round((endTime - DateTime.Now).TotalMinutes));
        }


        // Rounds a DateTime up to the nearest 5-minute mark.
        public static DateTime RoundUpToNearest5Minutes(DateTime dt)
        {
            // Number of ticks in 5 minutes.
            const long ticksPer5Min = TimeSpan.TicksPerMinute * 5;

            // Determine how many ticks past the last 5-minute interval.
            long remainder = dt.Ticks % ticksPer5Min;
            if (remainder != 0)
            {
                // Round up by removing the remainder and adding one full interval.
                dt = new DateTime(dt.Ticks - remainder + ticksPer5Min);
            }

            // Ensure that seconds and milliseconds are reset to zero.
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
        }

        // Generates a list of DateTime values at 5-minute intervals between start and end.
        public static List<DateTime> GenerateTimeIntervals(DateTime start, DateTime end)
        {
            List<DateTime> intervals = new List<DateTime>();

            // Round the start time up to the nearest 5 minutes.
            DateTime current = RoundUpToNearest5Minutes(start);

            // Loop adding 5 minutes at a time until we pass the end time.
            while (current <= end)
            {
                intervals.Add(current);
                current = current.AddMinutes(5);
            }

            return intervals;
        }
    }
}