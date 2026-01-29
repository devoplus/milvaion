using Milvasoft.DataAccess.EfCore.Bulk;

namespace Milvaion.Application.Interfaces.Redis;

/// <summary>
/// Redis-based real-time statistics service for dashboard metrics.
/// Provides atomic counter operations for job occurrence tracking.
/// </summary>
public interface IRedisStatsService
{
    /// <summary>
    /// Increments total occurrence counter.
    /// </summary>
    Task IncrementTotalOccurrencesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments status-specific counter atomically.
    /// </summary>
    /// <param name="status">Job occurrence status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task IncrementStatusCounterAsync(JobOccurrenceStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements status-specific counter atomically.
    /// </summary>
    /// <param name="status">Job occurrence status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DecrementStatusCounterAsync(JobOccurrenceStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates counters when status changes (decrement old, increment new).
    /// </summary>
    /// <param name="oldStatus">Previous status</param>
    /// <param name="newStatus">New status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateStatusCountersAsync(JobOccurrenceStatus oldStatus, JobOccurrenceStatus newStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current statistics from Redis counters.
    /// </summary>
    /// <returns>Dictionary of status counters</returns>
    Task<Dictionary<string, long>> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all counters (for testing or maintenance).
    /// </summary>
    Task ResetCountersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes counters from database (one-time sync on startup or after reset).
    /// </summary>
    /// <param name="context">Database context for querying current counts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SyncCountersFromDatabaseAsync(IMilvaBulkDbContextBase context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks job execution in timeline (for EPS/EPM calculation).
    /// Adds occurrence to sorted set with current timestamp as score.
    /// </summary>
    Task TrackExecutionAsync(Guid occurrenceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks completed job duration for average calculation.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds</param>
    /// <param name="cancellationToken"></param>
    Task TrackDurationAsync(long durationMs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets executions per minute (last 60 seconds).
    /// </summary>
    Task<double> GetExecutionsPerMinuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets average duration of completed jobs.
    /// </summary>
    Task<double?> GetAverageDurationAsync(CancellationToken cancellationToken = default);
}
