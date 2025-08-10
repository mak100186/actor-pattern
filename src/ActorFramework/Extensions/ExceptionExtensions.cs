using System.Globalization;

namespace ActorFramework.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    /// Returns a human‐readable exception description, or null if no exception.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <param name="pausedAtUtc">
    /// The UTC timestamp when the exception was observed; 
    /// will be converted to local time in the output.
    /// </param>
    /// <returns>
    /// Null if <paramref name="exception"/> is null; otherwise
    /// "{ExceptionType} occurred at {LocalTime}: {Exception.Message}".
    /// </returns>
    public static string? GetExceptionText(this Exception? exception, DateTimeOffset? pausedAtUtc)
    {
        if (exception is null || pausedAtUtc is null)
        {
            return "No exceptions recorded";
        }

        // Convert the paused‐at UTC timestamp to local time and format
        var localTime = pausedAtUtc.Value
            .ToLocalTime()
            .ToString("h:mm tt", CultureInfo.CurrentCulture);

        // Use the exception's runtime type name
        var typeName = exception.GetType().Name;
        var message = exception.Message;

        return $"{typeName} occurred at {localTime}: {message}";
    }
}