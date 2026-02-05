using Milvasoft.Milvaion.Sdk.Domain.Enums;

namespace Milvasoft.Milvaion.Sdk.Utils;

public static class MilvaionSdkExtensions
{
    /// <summary>
    /// Determines whether the JobOccurrenceStatus is a final status.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool IsFinalStatus(this JobOccurrenceStatus status)
        => status is JobOccurrenceStatus.Completed
                  or JobOccurrenceStatus.Failed
                  or JobOccurrenceStatus.Cancelled
                  or JobOccurrenceStatus.TimedOut
                  or JobOccurrenceStatus.Unknown;

    /// <summary>
    /// Converts an interval in seconds to an approximate cron expression.
    /// Note: Not all intervals can be exactly represented in cron format.
    /// Uses 6-field cron format: seconds minutes hours dayOfMonth month dayOfWeek
    /// </summary>
    /// <param name="intervalSeconds">Interval in seconds.</param>
    /// <returns>Cron expression string, or null if interval is invalid.</returns>
    public static string IntervalToCron(int intervalSeconds)
    {
        if (intervalSeconds <= 0)
            return null;

        // Seconds (1-59)
        if (intervalSeconds < 60)
        {
            // Every N seconds
            return $"*/{intervalSeconds} * * * * *";
        }

        var intervalMinutes = intervalSeconds / 60;
        var remainingSeconds = intervalSeconds % 60;

        // Minutes (1-59 minutes, no remaining seconds)
        if (intervalMinutes < 60 && remainingSeconds == 0)
        {
            if (intervalMinutes == 1)
                return "0 * * * * *"; // Every minute

            return $"0 */{intervalMinutes} * * * *"; // Every N minutes
        }

        var intervalHours = intervalMinutes / 60;
        var remainingMinutes = intervalMinutes % 60;

        // Hours (1-23 hours, no remaining minutes)
        if (intervalHours < 24 && remainingMinutes == 0 && remainingSeconds == 0)
        {
            if (intervalHours == 1)
                return "0 0 * * * *"; // Every hour

            return $"0 0 */{intervalHours} * * *"; // Every N hours
        }

        var intervalDays = intervalHours / 24;
        var remainingHours = intervalHours % 24;

        // Days (no remaining hours)
        if (intervalDays >= 1 && remainingHours == 0 && remainingMinutes == 0 && remainingSeconds == 0)
        {
            if (intervalDays == 1)
                return "0 0 0 * * *"; // Every day at midnight

            return $"0 0 0 */{intervalDays} * *"; // Every N days
        }

        // For complex intervals that don't fit cleanly, approximate to nearest minute
        if (intervalSeconds >= 60)
        {
            var approxMinutes = (int)Math.Round(intervalSeconds / 60.0);

            if (approxMinutes < 60)
                return $"0 */{approxMinutes} * * * *";

            var approxHours = (int)Math.Round(approxMinutes / 60.0);

            if (approxHours < 24)
                return $"0 0 */{approxHours} * * *";
        }

        // Fallback: treat as seconds if small enough
        return $"*/{intervalSeconds} * * * * *";
    }

    /// <summary>
    /// Gets the effective cron expression from either CronExpression or IntervalSeconds.
    /// </summary>
    /// <param name="cronExpression">Explicit cron expression.</param>
    /// <param name="intervalSeconds">Interval in seconds (used if cronExpression is null).</param>
    /// <returns>Cron expression string.</returns>
    public static string GetEffectiveCron(string cronExpression, int? intervalSeconds)
    {
        if (!string.IsNullOrWhiteSpace(cronExpression))
            return cronExpression;

        if (intervalSeconds.HasValue && intervalSeconds.Value > 0)
            return IntervalToCron(intervalSeconds.Value);

        return null;
    }
}
