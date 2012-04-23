using System;
using System.Globalization;

namespace Communication.BaseSystem
{
    public static class Times
    {
        public static String DateTimeUtcToString(this DateTime DateTimeUtc)
        {
            if (DateTimeUtc.Kind != DateTimeKind.Utc) { throw new ArgumentException(); }
            return DateTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        public static DateTime StringToDateTimeUtc(this String s)
        {
            return DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }
        public static String DateTimeUtcWithMillisecondsToString(this DateTime DateTimeUtc)
        {
            if (DateTimeUtc.Kind != DateTimeKind.Utc) { throw new ArgumentException(); }
            return DateTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
        public static DateTime StringToDateTimeUtcWithMillisecond(this String s)
        {
            return DateTime.ParseExact(s, "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }
        public static TimeSpan StringToTimeUtc(this String s)
        {
            return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
        }
        public static DateTime FloorToFullMinute(this DateTime t)
        {
            if (t.Second != 0 || t.Millisecond != 0)
            {
                var nt = t;
                return new DateTime(nt.Year, nt.Month, nt.Day, nt.Hour, nt.Minute, 0, 0, nt.Kind);
            }
            else
            {
                return t;
            }
        }
        public static DateTime CeilToFullMinute(this DateTime t)
        {
            if (t.Second != 0 || t.Millisecond != 0)
            {
                var nt = t + new TimeSpan(0, 1, 0);
                return new DateTime(nt.Year, nt.Month, nt.Day, nt.Hour, nt.Minute, 0, 0, nt.Kind);
            }
            else
            {
                return t;
            }
        }
        public static DateTime GetPreviousTime(this DateTime t, TimeSpan TimeOfDay)
        {
            if (t.Kind != DateTimeKind.Utc) { throw new ArgumentException(); }
            Func<DateTime> TodayTime = () =>
            {
                var nt = t;
                return new DateTime(nt.Year, nt.Month, nt.Day, TimeOfDay.Hours, TimeOfDay.Minutes, TimeOfDay.Seconds, TimeOfDay.Milliseconds, nt.Kind);
            };
            Func<DateTime> YesterdayTime = () =>
            {
                var nt = t - new TimeSpan(1, 0, 0, 0);
                return new DateTime(nt.Year, nt.Month, nt.Day, TimeOfDay.Hours, TimeOfDay.Minutes, TimeOfDay.Seconds, TimeOfDay.Milliseconds, nt.Kind);
            };

            if (t.Hour > TimeOfDay.Hours)
            {
                return TodayTime();
            }
            else if (t.Hour < TimeOfDay.Hours)
            {
                return YesterdayTime();
            }
            if (t.Minute > TimeOfDay.Minutes)
            {
                return TodayTime();
            }
            else if (t.Minute < TimeOfDay.Minutes)
            {
                return YesterdayTime();
            }
            if (t.Second > TimeOfDay.Seconds)
            {
                return TodayTime();
            }
            else if (t.Second < TimeOfDay.Seconds)
            {
                return YesterdayTime();
            }
            if (t.Millisecond > TimeOfDay.Milliseconds)
            {
                return TodayTime();
            }
            else if (t.Millisecond < TimeOfDay.Milliseconds)
            {
                return YesterdayTime();
            }
            return TodayTime();
        }
        public static TimeSpan Max(TimeSpan x, TimeSpan y)
        {
            if (x.TotalMilliseconds >= y.TotalMilliseconds)
            {
                return x;
            }
            else
            {
                return y;
            }
        }
        public static TimeSpan Min(TimeSpan x, TimeSpan y)
        {
            if (x.TotalMilliseconds <= y.TotalMilliseconds)
            {
                return x;
            }
            else
            {
                return y;
            }
        }
    }
}
