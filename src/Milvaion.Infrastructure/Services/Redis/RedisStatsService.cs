using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Milvaion.Application.Interfaces.Redis;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Bulk;
using Milvasoft.Milvaion.Sdk.Utils;
using StackExchange.Redis;

namespace Milvaion.Infrastructure.Services.Redis;

/// <summary>
/// Redis-based real-time statistics service.
/// Uses atomic INCR/DECR operations for thread-safe counters.
/// Multi-instance safe with Lua scripts and distributed locks.
/// </summary>
public class RedisStatsService(IConnectionMultiplexer redis,
                               IRedisCircuitBreaker circuitBreaker,
                               ILoggerFactory loggerFactory) : IRedisStatsService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly IRedisCircuitBreaker _circuitBreaker = circuitBreaker;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<RedisStatsService>();

    private const string _keyPrefix = "stats:occurrences:";
    private const string _totalKey = _keyPrefix + "total";
    private const string _queuedKey = _keyPrefix + "queued";
    private const string _runningKey = _keyPrefix + "running";
    private const string _completedKey = _keyPrefix + "completed";
    private const string _failedKey = _keyPrefix + "failed";
    private const string _cancelledKey = _keyPrefix + "cancelled";
    private const string _timedOutKey = _keyPrefix + "timedout";
    private const string _timelineKey = "stats:timeline"; // ZSET for time-based queries
    private const string _durationSumKey = _keyPrefix + "duration_sum"; // Total duration in ms
    private const string _durationCountKey = _keyPrefix + "duration_count"; // Count of completed jobs with duration
    private const string _syncLockKey = "stats:sync:lock"; // Distributed lock for sync operations

    // Lua script for atomic decrement with lower bound check (prevents negative values)
    private const string _decrementWithFloorScript = @"
        local current = redis.call('GET', KEYS[1])
        if not current then
            return 0
        end
        current = tonumber(current)
        if current > 0 then
            return redis.call('DECR', KEYS[1])
        end
        return 0";

    // Lua script for atomic update status counters (decrement old, increment new, both with floor check)
    private const string _updateStatusScript = @"
        local oldKey = KEYS[1]
        local newKey = KEYS[2]

        -- Decrement old status counter (with floor check)
        if oldKey and oldKey ~= '' then
            local oldVal = redis.call('GET', oldKey)
            if oldVal then
                oldVal = tonumber(oldVal)
                if oldVal > 0 then
                    redis.call('DECR', oldKey)
                end
            end
        end

        -- Increment new status counter
        if newKey and newKey ~= '' then
            redis.call('INCR', newKey)
        end

        return 1";

    /// <inheritdoc/>
    public Task IncrementTotalOccurrencesAsync(CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            await _db.StringIncrementAsync(_totalKey);
            return true;
        },
        fallback: async () => true,
        operationName: "IncrementTotalOccurrences",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task IncrementStatusCounterAsync(JobOccurrenceStatus status, CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var key = GetKeyForStatus(status);
            if (key != null)
                await _db.StringIncrementAsync(key);
            return true;
        },
        fallback: async () => true,
        operationName: "IncrementStatusCounter",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task DecrementStatusCounterAsync(JobOccurrenceStatus status, CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var key = GetKeyForStatus(status);

            if (key != null)
            {
                // Use Lua script for atomic decrement with floor check (prevents negative values)
                await _db.ScriptEvaluateAsync(_decrementWithFloorScript, [key]);
            }

            return true;
        },
        fallback: async () => true,
        operationName: "DecrementStatusCounter",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task UpdateStatusCountersAsync(JobOccurrenceStatus oldStatus, JobOccurrenceStatus newStatus, CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var oldKey = GetKeyForStatus(oldStatus);
            var newKey = GetKeyForStatus(newStatus);

            // Use Lua script for atomic update (prevents race conditions between decrement and increment)
            await _db.ScriptEvaluateAsync(
                _updateStatusScript,
                [oldKey ?? "", newKey ?? ""]
            );

            return true;
        },
        fallback: async () => true,
        operationName: "UpdateStatusCounters",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task<Dictionary<string, long>> GetStatisticsAsync(CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var keys = new RedisKey[]
            {
                _totalKey, _queuedKey, _runningKey, _completedKey, _failedKey, _cancelledKey, _timedOutKey,
                _durationSumKey, _durationCountKey
            };
            var values = await _db.StringGetAsync(keys);

            return new Dictionary<string, long>
            {
                ["Total"] = values[0].HasValue ? (long)values[0] : 0,
                ["Queued"] = values[1].HasValue ? (long)values[1] : 0,
                ["Running"] = values[2].HasValue ? (long)values[2] : 0,
                ["Completed"] = values[3].HasValue ? (long)values[3] : 0,
                ["Failed"] = values[4].HasValue ? (long)values[4] : 0,
                ["Cancelled"] = values[5].HasValue ? (long)values[5] : 0,
                ["TimedOut"] = values[6].HasValue ? (long)values[6] : 0,
                ["DurationSum"] = values[7].HasValue ? (long)values[7] : 0,
                ["DurationCount"] = values[8].HasValue ? (long)values[8] : 0
            };
        },
        fallback: async () => [],
        operationName: "GetStatistics",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task ResetCountersAsync(CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var keys = new RedisKey[]
            {
                _totalKey, _queuedKey, _runningKey, _completedKey, _failedKey, _cancelledKey, _timedOutKey,
                _durationSumKey, _durationCountKey, _timelineKey
            };
            await _db.KeyDeleteAsync(keys);
            _logger.Information("All statistics counters reset (including timeline and duration)");
            return true;
        },
        fallback: async () => true,
        operationName: "ResetCounters",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public async Task SyncCountersFromDatabaseAsync(IMilvaBulkDbContextBase context, CancellationToken cancellationToken = default)
    {
        // Use distributed lock to prevent multiple instances from syncing simultaneously
        var lockToken = Guid.NewGuid().ToString();
        var lockAcquired = await _db.StringSetAsync(_syncLockKey, lockToken, TimeSpan.FromMinutes(5), When.NotExists);

        if (!lockAcquired)
        {
            _logger.Information("Stats sync already in progress by another instance, skipping...");
            return;
        }

        try
        {
            _logger.Information("Starting stats counter sync from database (lock acquired)...");

            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            var thirtySecondsAgo = now.AddSeconds(-30);

            // Use the same query logic as original dashboard (last 7 days, completed calculated)
            var sql = $@"
                 WITH stats AS (
                     SELECT
                         COUNT(*) AS ""TotalExecutions"",
                         COUNT(*) FILTER (WHERE ""Status"" = 0) AS ""QueuedJobs"",
                         COUNT(*) FILTER (WHERE ""Status"" = 1) AS ""RunningJobs"",
                         COUNT(*) FILTER (WHERE ""Status"" = 3) AS ""FailedJobs"",
                         COUNT(*) FILTER (WHERE ""Status"" = 4) AS ""CancelledJobs"",
                         COUNT(*) FILTER (WHERE ""Status"" = 5) AS ""TimedOutJobs"",
                         COALESCE(SUM(""DurationMs"") FILTER (WHERE ""Status"" = 2 AND ""DurationMs"" IS NOT NULL), 0) AS ""TotalDuration"",
                         COUNT(*) FILTER (WHERE ""Status"" = 2 AND ""DurationMs"" IS NOT NULL) AS ""DurationCount""
                     FROM ""JobOccurrences""
                     WHERE ""CreatedAt"" >= {{0}}
                 ),
                 recent AS (
                     SELECT COUNT(*) AS ""RecentCount""
                     FROM ""JobOccurrences""
                     WHERE ""CreatedAt"" >= {{1}}
                 )
                 SELECT
                     s.""TotalExecutions"",
                     s.""QueuedJobs"",
                     s.""RunningJobs"",
                     s.""FailedJobs"",
                     s.""CancelledJobs"",
                     s.""TimedOutJobs"",
                     s.""TotalDuration"",
                     s.""DurationCount"",
                     r.""RecentCount""
                 FROM stats s, recent r";

            var result = await context.Database.SqlQueryRaw<StatsSyncDto>(sql, sevenDaysAgo, thirtySecondsAgo).FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                _logger.Warning("No data returned from database for stats sync");
                return;
            }

            // Calculate completed: Total - (Queued + Running + Failed + Cancelled + TimedOut)
            var completed = result.TotalExecutions - result.QueuedJobs - result.RunningJobs - result.FailedJobs - result.CancelledJobs - result.TimedOutJobs;
            if (completed < 0)
                completed = 0;

            // Delete and set new values atomically using transaction
            var transaction = _db.CreateTransaction();

            // Delete old keys
            var keys = new RedisKey[]
            {
                _totalKey, _queuedKey, _runningKey, _completedKey, _failedKey, _cancelledKey, _timedOutKey,
                _durationSumKey, _durationCountKey, _timelineKey
            };
            _ = transaction.KeyDeleteAsync(keys);

            // Set new values
            _ = transaction.StringSetAsync(_totalKey, result.TotalExecutions);
            _ = transaction.StringSetAsync(_queuedKey, result.QueuedJobs);
            _ = transaction.StringSetAsync(_runningKey, result.RunningJobs);
            _ = transaction.StringSetAsync(_completedKey, completed);
            _ = transaction.StringSetAsync(_failedKey, result.FailedJobs);
            _ = transaction.StringSetAsync(_cancelledKey, result.CancelledJobs);
            _ = transaction.StringSetAsync(_timedOutKey, result.TimedOutJobs);
            _ = transaction.StringSetAsync(_durationSumKey, result.TotalDuration);
            _ = transaction.StringSetAsync(_durationCountKey, result.DurationCount);

            await transaction.ExecuteAsync();

            _logger.Information("Stats synced (last 7 days). Total: {Total}, Completed: {Completed}, Failed: {Failed}, AvgDuration: {AvgDuration}ms",
                result.TotalExecutions, completed, result.FailedJobs,
                result.DurationCount > 0 ? result.TotalDuration / result.DurationCount : 0);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to sync stats counters from database");
            throw;
        }
        finally
        {
            // Release distributed lock (only if we still own it)
            var script = @"
                if redis.call('GET', KEYS[1]) == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                else
                    return 0
                end";
            await _db.ScriptEvaluateAsync(script, [_syncLockKey], [lockToken]);
        }
    }

    /// <inheritdoc/>
    public Task TrackExecutionAsync(Guid occurrenceId, CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await _db.SortedSetAddAsync(_timelineKey, occurrenceId.ToString(), now);

            // Auto-cleanup: Remove entries older than 5 minutes (keep memory bounded)
            var fiveMinutesAgo = now - (5 * 60 * 1000);
            await _db.SortedSetRemoveRangeByScoreAsync(_timelineKey, 0, fiveMinutesAgo);

            return true;
        },
        fallback: async () => true,
        operationName: "TrackExecution",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task TrackDurationAsync(long durationMs, CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var transaction = _db.CreateTransaction();
            _ = transaction.StringIncrementAsync(_durationSumKey, durationMs);
            _ = transaction.StringIncrementAsync(_durationCountKey, 1);
            await transaction.ExecuteAsync();
            return true;
        },
        fallback: async () => true,
        operationName: "TrackDuration",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task<double> GetExecutionsPerMinuteAsync(CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var oneMinuteAgo = now - (60 * 1000);

            var count = await _db.SortedSetLengthAsync(_timelineKey, oneMinuteAgo, now);
            return (double)count;
        },
        fallback: async () => 0.0,
        operationName: "GetExecutionsPerMinute",
        cancellationToken: cancellationToken
    );

    /// <inheritdoc/>
    public Task<double?> GetAverageDurationAsync(CancellationToken cancellationToken = default) => _circuitBreaker.ExecuteAsync(
        operation: async () =>
        {
            var values = await _db.StringGetAsync([_durationSumKey, _durationCountKey]);

            var sum = values[0].HasValue ? (long)values[0] : 0;
            var count = values[1].HasValue ? (long)values[1] : 0;

            if (count == 0)
                return (double?)null;

            return (double)sum / count;
        },
        fallback: async () => (double?)null,
        operationName: "GetAverageDuration",
        cancellationToken: cancellationToken
    );

    private static string GetKeyForStatus(JobOccurrenceStatus status) => status switch
    {
        JobOccurrenceStatus.Queued => _queuedKey,
        JobOccurrenceStatus.Running => _runningKey,
        JobOccurrenceStatus.Completed => _completedKey,
        JobOccurrenceStatus.Failed => _failedKey,
        JobOccurrenceStatus.Cancelled => _cancelledKey,
        JobOccurrenceStatus.TimedOut => _timedOutKey,
        _ => null
    };
}

/// <summary>
/// DTO for stats sync query result.
/// </summary>
internal class StatsSyncDto
{
    public long TotalExecutions { get; set; }
    public long QueuedJobs { get; set; }
    public long RunningJobs { get; set; }
    public long FailedJobs { get; set; }
    public long CancelledJobs { get; set; }
    public long TimedOutJobs { get; set; }
    public long TotalDuration { get; set; }
    public long DurationCount { get; set; }
    public long RecentCount { get; set; }
}