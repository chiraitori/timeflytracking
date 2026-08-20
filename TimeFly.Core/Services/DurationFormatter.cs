namespace TimeFly.Core.Services;

public static class DurationFormatter
{
    public static string Compact(long seconds)
    {
        seconds = Math.Max(0, seconds);
        var duration = TimeSpan.FromSeconds(seconds);

        if (duration.TotalHours >= 1)
        {
            return duration.Minutes > 0
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"{(int)duration.TotalHours}h";
        }

        return duration.Minutes > 0 ? $"{duration.Minutes}m" : $"{duration.Seconds}s";
    }
}

