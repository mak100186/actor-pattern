namespace ActorFramework.Extensions;

public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Returns a string like "5 minutes ago at 3:15 PM" (using the caller's local time).
    /// </summary>
    public static string ToRelativeTimeWithLocal(this DateTimeOffset past, DateTimeOffset? relativeTo = null)
    {
        if (past == DateTimeOffset.MinValue)
        {
            return "Never";
        }

        // Compute span from now (in UTC) to the past timestamp
        var nowUtc = (relativeTo ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var span = nowUtc - past.ToUniversalTime();

        // Determine the relative-time part
        string relative;
        if (span.TotalSeconds < 0)
        {
            relative = "just now";
        }
        else if (span.TotalSeconds < 60)
        {
            var sec = (int)span.TotalSeconds;
            relative = $"{sec} second{Plural(sec)} ago";
        }
        else if (span.TotalMinutes < 60)
        {
            var min = (int)span.TotalMinutes;
            relative = $"{min} minute{Plural(min)} ago";
        }
        else if (span.TotalHours < 24)
        {
            var hr = (int)span.TotalHours;
            relative = $"{hr} hour{Plural(hr)} ago";
        }
        else
        {
            var days = (int)span.TotalDays;
            relative = $"{days} day{Plural(days)} ago";
        }

        // Convert the original timestamp to local time for display
        var localTime = past.ToLocalTime();
        // Format e.g. "3:15 PM" – adjust format string to taste
        var localString = localTime.ToString("h:mm tt");

        return $"{relative} at {localString}";
    }

    private static string Plural(double value) => value >= 2 ? "s" : string.Empty;
}