namespace FarmGrid.Models
{
    /// <summary>
    /// FarmGrid stores instants in UTC and shows them in India Standard Time.
    /// Calendar dates (e.g. a Trip's dispatch date) are compared with today's date in India.
    /// </summary>
    public static class IndiaTime
    {
        public static readonly TimeZoneInfo Zone = FindZone();

        private static TimeZoneInfo FindZone()
        {
            foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                {
                    return zone;
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
        }

        /// <summary>Today's calendar date in India.</summary>
        public static DateTime Today(TimeProvider time) =>
            TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).Date;

        /// <summary>Converts a stored UTC instant to India time for display.</summary>
        public static DateTime ToIndiaTime(this DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

        /// <summary>A stored UTC instant as an ISO 8601 string with a "Z", safe for JavaScript's Date.</summary>
        public static string ToUtcIso(this DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");
    }
}
