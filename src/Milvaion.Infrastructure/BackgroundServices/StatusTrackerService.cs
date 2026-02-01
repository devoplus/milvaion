using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Constants;
using Milvaion.Infrastructure.BackgroundServices.Base;
using Milvaion.Infrastructure.Persistence.Context;
using Milvaion.Infrastructure.Telemetry;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text.Json;

namespace Milvaion.Infrastructure.BackgroundServices;

/// <summary>
/// Consumes job status updates from RabbitMQ and updates JobOccurrence records.
/// </summary>
public class StatusTrackerService(IServiceProvider serviceProvider,
                                  IRedisSchedulerService redisScheduler,
                                  IRedisStatsService redisStatsService,
                                  IOptions<RabbitMQOptions> rabbitOptions,
                                  IOptions<StatusTrackerOptions> options,
                                  IOptions<JobAutoDisableOptions> autoDisableOptions,
                                  ILoggerFactory loggerFactory,
                                  BackgroundServiceMetrics metrics,
                                  IMemoryStatsRegistry memoryStatsRegistry = null) : MemoryTrackedBackgroundService(loggerFactory, options.Value, memoryStatsRegistry)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IRedisSchedulerService _redisScheduler = redisScheduler;
    private readonly IRedisStatsService _redisStatsService = redisStatsService;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<StatusTrackerService>();
    private readonly RabbitMQOptions _rabbitOptions = rabbitOptions.Value;
    private readonly StatusTrackerOptions _options = options.Value;
    private readonly JobAutoDisableOptions _autoDisableOptions = autoDisableOptions.Value;
    private readonly BackgroundServiceMetrics _metrics = metrics;
    private IConnection _connection;
    private IChannel _channel;
    private readonly JobOccurrenceStatus[] _finalStatuses =
    [
        JobOccurrenceStatus.Completed,
        JobOccurrenceStatus.Failed,
        JobOccurrenceStatus.Cancelled,
        JobOccurrenceStatus.TimedOut
    ];

    private readonly JobOccurrenceStatus[] _nonFinalStatuses =
    [
        JobOccurrenceStatus.Queued,
        JobOccurrenceStatus.Running
    ];

    //  Batch processing
    private readonly System.Collections.Concurrent.ConcurrentQueue<JobStatusUpdateMessage> _statusBatch = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private readonly static List<string> _updatePropNames =
    [
        nameof(JobOccurrence.Status),
        nameof(JobOccurrence.WorkerId),
        nameof(JobOccurrence.StartTime),
        nameof(JobOccurrence.EndTime),
        nameof(JobOccurrence.DurationMs),
        nameof(JobOccurrence.Result),
        nameof(JobOccurrence.Exception),
        nameof(JobOccurrence.LastHeartbeat),
        nameof(JobOccurrence.StatusChangeLogs)
    ];

    private readonly static List<string> _jobCircuitBreakerUpdateProps =
    [
        nameof(ScheduledJob.AutoDisableSettings),
        nameof(ScheduledJob.IsActive)
    ];

    /// <inheritdoc/>
    protected override string ServiceName => "StatusTracker";

    /// <inheritdoc />
    protected override async Task ExecuteWithMemoryTrackingAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.Warning("Status tracking is disabled. Skipping startup.");

            return;
        }

        _logger.Information("Status tracking is starting...");

        // Start batch processor task
        var batchTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_options.BatchIntervalMs, stoppingToken);

                await ProcessBatchAsync(stoppingToken);

                TrackMemoryAfterIteration();
            }
        }, stoppingToken);

        var retryCount = 0;
        const int maxRetries = 10;
        const int retryDelaySeconds = 5;

        while (!stoppingToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);

                // If we reach here, connection was successful
                retryCount = 0;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Status tracking is shutting down");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;

                _logger.Error(ex, "StatusTrackerService connection failed (attempt {Retry}/{MaxRetries})", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.Fatal("StatusTrackerService failed to connect after {MaxRetries} attempts. Service will be disabled until application restart.", maxRetries);
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds * retryCount), stoppingToken);
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        // Setup RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Host,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.Username,
            Password = _rabbitOptions.Password,
            VirtualHost = _rabbitOptions.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            TopologyRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);

        // Subscribe to connection events
        _connection.ConnectionShutdownAsync += async (sender, args) =>
        {
            _logger.Warning("StatusTracker RabbitMQ connection shutdown. Reason: {Reason}", args.ReplyText);
            await Task.CompletedTask;
        };

        _connection.RecoverySucceededAsync += async (sender, args) =>
        {
            _logger.Information("StatusTracker RabbitMQ connection recovered successfully");
            await Task.CompletedTask;
        };

        _connection.ConnectionRecoveryErrorAsync += async (sender, args) =>
        {
            _logger.Error(args.Exception, "StatusTracker RabbitMQ connection recovery failed");
            await Task.CompletedTask;
        };

        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Subscribe to channel events
        _channel.ChannelShutdownAsync += async (sender, args) =>
        {
            _logger.Warning("StatusTracker RabbitMQ channel shutdown. Reason: {Reason}", args.ReplyText);
            await Task.CompletedTask;
        };

        // Declare queue (idempotent)
        await _channel.QueueDeclareAsync(queue: WorkerConstant.Queues.StatusUpdates,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: stoppingToken);

        // Set prefetch count
        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        _logger.Information("StatusTracker connected to RabbitMQ. Queue: {Queue}", WorkerConstant.Queues.StatusUpdates);

        // Setup consumer with all event handlers
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                await ProcessStatusUpdateAsync(ea, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in StatusTracker consumer handler for CorrelationId: {CorrelationId}", ea.BasicProperties?.CorrelationId);
            }
        };

        consumer.ShutdownAsync += async (sender, args) =>
        {
            _logger.Warning("StatusTracker consumer shutdown. Reason: {Reason}", args.ReplyText);
            await Task.CompletedTask;
        };

        consumer.RegisteredAsync += async (sender, args) =>
        {
            _logger.Information("StatusTracker consumer registered. ConsumerTags: {Tags}", string.Join(", ", args.ConsumerTags));
            await Task.CompletedTask;
        };

        consumer.UnregisteredAsync += async (sender, args) =>
        {
            _logger.Warning("StatusTracker consumer unregistered. ConsumerTags: {Tags}", string.Join(", ", args.ConsumerTags));
            await Task.CompletedTask;
        };

        var consumerTag = await _channel.BasicConsumeAsync(queue: WorkerConstant.Queues.StatusUpdates,
                                                           autoAck: false,
                                                           consumer: consumer,
                                                           cancellationToken: stoppingToken);

        _logger.Information("StatusTrackerService consuming from {Queue}. ConsumerTag: {ConsumerTag}, ChannelOpen: {IsOpen}, ChannelNumber: {ChannelNumber}", WorkerConstant.Queues.StatusUpdates, consumerTag, _channel.IsOpen, _channel.ChannelNumber);

        // Keep running until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("StatusTracker consumer loop cancelled gracefully");
            throw;
        }
    }

    private async Task ProcessStatusUpdateAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            // Parse message
            var message = JsonSerializer.Deserialize<JobStatusUpdateMessage>(ea.Body.Span, ConstantJsonOptions.PropNameCaseInsensitive);

            if (message == null)
            {
                _logger.Debug("Failed to deserialize status update message");

                await _channel.BasicNackAsync(ea.DeliveryTag, false, false, cancellationToken);

                return;
            }

            //  ONLY mark as Running immediately (for dispatcher checks)
            if (message.Status == JobOccurrenceStatus.Running)
            {
                try
                {
                    await _redisScheduler.TryMarkJobAsRunningAsync(message.JobId, message.CorrelationId, cancellationToken);
                    _logger.Debug("Job {JobId} immediately marked as running in Redis (CorrelationId: {CorrelationId})", message.JobId, message.CorrelationId);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to immediately mark job {JobId} as running in Redis (non-critical)", message.JobId);
                }
            }

            //  Add to batch queue (NO DB operation!)
            _statusBatch.Enqueue(message);

            //  Trigger immediate batch if full
            if (_statusBatch.Count >= _options.BatchSize)
                await ProcessBatchAsync(cancellationToken);

            // ACK the message (safe operation)
            await SafeAckAsync(ea.DeliveryTag, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process status update message");

            await SafeNackAsync(ea.DeliveryTag, cancellationToken);
        }
    }

    /// <summary>
    /// Process batch of status updates - single DB transaction.
    /// </summary>
    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (_statusBatch.IsEmpty)
            return;

        var sw = Stopwatch.StartNew();

        //  Use semaphore to ensure single access
        await _batchLock.WaitAsync(cancellationToken);

        try
        {
            if (_statusBatch.IsEmpty)
                return;

            var batch = new List<JobStatusUpdateMessage>();

            while (_statusBatch.TryDequeue(out var message))
            {
                batch.Add(message);
            }

            if (batch.Count == 0)
                return;

            _metrics.SetStatusBatchSize(batch.Count);

            // Retry logic for optimistic concurrency conflicts
            const int maxRetries = 3;
            var retryCount = 0;
            var retryDelay = TimeSpan.FromMilliseconds(50); // Start with 50ms

            while (retryCount < maxRetries)
            {
                try
                {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MilvaionDbContext>();

                    // Deduplicate by CorrelationId (last update wins)
                    var statusByCorrelation = new Dictionary<Guid, JobStatusUpdateMessage>(batch.Count);

                    // Last update wins - overwrite if already exists
                    foreach (var message in batch)
                        statusByCorrelation[message.CorrelationId] = message;

                    var correlationIds = statusByCorrelation.Keys.ToList();

                    _logger.Debug("Processing batch: {Count} unique status updates. Sample CorrelationIds: {Samples}", correlationIds.Count, string.Join(", ", correlationIds.Take(3)));

                    //  Single query for all occurrences (ORDER BY Id to prevent deadlock)
                    var occurrences = await dbContext.JobOccurrences
                                                     .AsNoTracking()
                                                     .Where(o => correlationIds.Contains(o.CorrelationId))
                                                     .OrderBy(o => o.Id) // Consistent lock order
                                                     .Select(JobOccurrence.Projections.UpdateStatus)
                                                     .ToListAsync(cancellationToken: cancellationToken);

                    if (occurrences.Count == 0)
                    {
                        _logger.Warning("No matching occurrences found for {Count} status updates. CorrelationIds: {Ids}", batch.Count, string.Join(", ", correlationIds.Take(5)));
                        return;
                    }

                    var occurrenceDict = occurrences.ToDictionary(o => o.CorrelationId);

                    // Track jobs that need circuit breaker updates
                    var jobsToUpdateCircuitBreaker = new Dictionary<Guid, (bool isFailed, string exception)>();

                    // Track consumer counter updates (batch processing)
                    var consumerCounterUpdates = new List<(string workerId, string instanceId, string jobType, JobOccurrenceStatus previousStatus, JobOccurrenceStatus newStatus)>();

                    //  Update in memory (NO foreach DB operation!)
                    foreach (var kvp in statusByCorrelation)
                    {
                        if (!occurrenceDict.TryGetValue(kvp.Key, out var occurrence))
                        {
                            _logger.Debug("JobOccurrence not found for CorrelationId: {CorrelationId}", kvp.Key);
                            continue;
                        }

                        var message = kvp.Value;

                        // Track status change history
                        var previousStatus = occurrence.Status;
                        var newStatus = message.Status;

                        // Prevent invalid status transitions from FINAL states
                        // Once a job reaches a terminal state (Completed, Failed, Cancelled, TimedOut), it should NOT transition back to Running or Queued (race condition from late messages)
                        if (_finalStatuses.Contains(previousStatus) && _nonFinalStatuses.Contains(newStatus))
                            continue;

                        // Detect if this is a heartbeat-only update (no status change, no other data)
                        var isHeartbeat = previousStatus == newStatus
                                          && newStatus == JobOccurrenceStatus.Running
                                          && message.EndTime == null
                                          && message.DurationMs == null
                                          && string.IsNullOrEmpty(message.Result);

                        if (isHeartbeat)
                        {
                            occurrence.LastHeartbeat = DateTime.UtcNow;

                            _logger.Debug("Heartbeat received for CorrelationId: {CorrelationId}", kvp.Key);

                            // No need to update Redis counters for heartbeat (status hasn't changed)
                            continue; // Skip other processing
                        }

                        occurrence.StatusChangeLogs ??= [];

                        // Limit status change logs to prevent unbounded growth
                        if (occurrence.StatusChangeLogs.Count >= _options.ExecutionLogMaxCount)
                            occurrence.StatusChangeLogs = [.. occurrence.StatusChangeLogs.OrderByDescending(l => l.Timestamp).Take(_options.ExecutionLogMaxCount)];

                        if (previousStatus != newStatus)
                            occurrence.StatusChangeLogs.Add(new OccurrenceStatusChangeLog
                            {
                                Timestamp = DateTime.UtcNow,
                                From = previousStatus,
                                To = newStatus
                            });

                        // Collect consumer counter updates for batch processing
                        if (!string.IsNullOrEmpty(message.WorkerId) && !string.IsNullOrEmpty(message.InstanceId) && !string.IsNullOrEmpty(occurrence.JobName) && previousStatus != newStatus)
                        {
                            consumerCounterUpdates.Add((message.WorkerId, message.InstanceId, occurrence.JobName, previousStatus, newStatus));
                        }

                        // Update real-time statistics counters for dashboard (SYNCHRONOUS for critical counters)
                        if (previousStatus != newStatus)
                        {
                            try
                            {
                                await _redisStatsService.UpdateStatusCountersAsync(previousStatus, newStatus, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning(ex, "Failed to update Redis stats counters. Dashboard stats may be stale. Status: {Old} -> {New}, CorrelationId: {CorrelationId}", previousStatus, newStatus, occurrence.CorrelationId);
                            }
                        }

                        // Track for circuit breaker update
                        if (newStatus == JobOccurrenceStatus.Failed)
                        {
                            jobsToUpdateCircuitBreaker[occurrence.JobId] = (true, message.Exception ?? "Unknown error");
                        }
                        else if (newStatus == JobOccurrenceStatus.Completed)
                        {
                            jobsToUpdateCircuitBreaker[occurrence.JobId] = (false, null);
                        }

                        occurrence.Status = message.Status;
                        occurrence.WorkerId = message.WorkerId;

                        if (message.StartTime.HasValue)
                            occurrence.StartTime = message.StartTime;

                        if (message.EndTime.HasValue)
                            occurrence.EndTime = message.EndTime;

                        if (message.DurationMs.HasValue)
                        {
                            occurrence.DurationMs = message.DurationMs;

                            // Track duration for average calculation (only for completed jobs)
                            if (newStatus == JobOccurrenceStatus.Completed)
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _redisStatsService.TrackDurationAsync(message.DurationMs.Value, cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.Debug(ex, "Failed to track duration (non-critical)");
                                    }
                                }, CancellationToken.None);
                            }
                        }

                        if (!string.IsNullOrEmpty(message.Result))
                            occurrence.Result = message.Result;

                        // Smart exception handling: Clear old exception if job completed successfully
                        if (!string.IsNullOrEmpty(message.Exception))
                        {
                            occurrence.Exception = message.Exception;
                        }
                        else if (message.Status == JobOccurrenceStatus.Completed)
                        {
                            // Job completed successfully - clear any previous exception
                            occurrence.Exception = null;
                        }

                        occurrence.LastHeartbeat = DateTime.UtcNow;

                    }

                    // Sort occurrences by Id before bulk update to prevent deadlock
                    occurrences = [.. occurrences.OrderBy(o => o.Id)];

                    // BulkUpdate with RowVersion concurrency check
                    await dbContext.BulkUpdateAsync(occurrences, (bc) =>
                    {
                        bc.PropertiesToIncludeOnUpdate = _updatePropNames;
                    }, cancellationToken: cancellationToken);

                    // Process circuit breaker updates for jobs
                    if (jobsToUpdateCircuitBreaker.Count > 0 && _autoDisableOptions.Enabled)
                        await ProcessCircuitBreakerUpdatesAsync(dbContext, jobsToUpdateCircuitBreaker, cancellationToken);

                    // Batch update consumer counters (SINGLE Redis Lua script call!)
                    if (consumerCounterUpdates.Count > 0)
                        await UpdateConsumerCountersBatchAsync(consumerCounterUpdates, cancellationToken);

                    // Batch Redis updates efficiently with deduplication
                    // Group by JobId to avoid duplicate Redis calls for same job
                    var redisUpdates = new Dictionary<Guid, (JobOccurrenceStatus status, Guid correlationId)>();

                    foreach (var occurrence in occurrences)
                    {
                        // Last update wins - if same JobId appears multiple times, use latest status
                        if (occurrence.Status == JobOccurrenceStatus.Running || occurrence.Status.IsFinalStatus())
                        {
                            redisUpdates[occurrence.JobId] = (occurrence.Status, occurrence.CorrelationId);
                        }
                    }

                    // Execute deduplicated Redis updates in parallel with timeout
                    if (redisUpdates.Count > 0)
                    {
                        var redisUpdateTasks = redisUpdates.Select(kvp => Task.Run(async () =>
                        {
                            var (status, correlationId) = kvp.Value;
                            var jobId = kvp.Key;

                            try
                            {
                                if (status == JobOccurrenceStatus.Running)
                                {
                                    // TryMarkJobAsRunningAsync is idempotent - no need to log if already marked
                                    await _redisScheduler.TryMarkJobAsRunningAsync(jobId, correlationId, cancellationToken);
                                }
                                else if (status.IsFinalStatus())
                                {
                                    await _redisScheduler.MarkJobAsCompletedAsync(jobId, cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Only log actual errors, not idempotent duplicate calls
                                _logger.Debug(ex, "Failed to update Redis for job {JobId} (non-critical)", jobId);
                            }
                        }, cancellationToken)).ToList();

                        var redisTimeout = Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                        var redisCompleted = await Task.WhenAny(Task.WhenAll(redisUpdateTasks), redisTimeout);

                        if (redisCompleted == redisTimeout)
                            _logger.Warning("Redis updates timed out after 3 seconds for {Count} deduplicated operations (from {Total} occurrences)", redisUpdates.Count, occurrences.Count);
                    }

                    // Parallel SignalR event publishing with timeout
                    var eventPublisher = scope.ServiceProvider.GetService<IJobOccurrenceEventPublisher>();

                    await eventPublisher.PublishOccurrenceUpdatedAsync(occurrences, _logger, cancellationToken);

                    // Record metrics
                    _metrics.RecordStatusUpdatesProcessed(batch.Count);
                    _metrics.RecordStatusUpdateDuration(sw.Elapsed.TotalMilliseconds, batch.Count);

                    // Record status distribution
                    foreach (var occ in occurrences)
                    {
                        _metrics.RecordStatusUpdateByStatus(occ.Status.ToString());
                    }

                    _logger.Debug("Processed {Count} status updates in batch (RetryCount: {RetryCount})", batch.Count, retryCount);

                    // SUCCESS - Exit retry loop
                    break;
                }
                catch (DbUpdateConcurrencyException concurrencyEx)
                {
                    retryCount++;

                    if (retryCount >= maxRetries)
                    {
                        _logger.Error(concurrencyEx, "Concurrency conflict after {MaxRetries} retries. Status updates will be retried in next batch.", maxRetries);

                        // Re-queue failed messages for next batch
                        foreach (var msg in batch)
                            _statusBatch.Enqueue(msg);

                        break;
                    }

                    _logger.Warning(concurrencyEx, "Concurrency conflict detected in status batch processing (Retry {RetryCount}/{MaxRetries}). Retrying after {Delay}ms...", retryCount, maxRetries, retryDelay.TotalMilliseconds);

                    // Exponential backoff: 50ms, 100ms, 200ms
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2);
                }
                catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "40P01") // Deadlock
                {
                    _logger.Warning(pgEx, "Deadlock detected in status batch processing. Updates will be retried in next batch.");

                    // Re-queue messages for next batch
                    foreach (var msg in batch)
                        _statusBatch.Enqueue(msg);

                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process status update batch");

                    // Re-queue messages for next batch
                    foreach (var msg in batch)
                        _statusBatch.Enqueue(msg);

                    break;
                }
            }
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Processes circuit breaker updates for jobs that completed or failed.
    /// Increments failure count on failure, resets on success, auto-disables if threshold reached.
    /// </summary>
    private async Task ProcessCircuitBreakerUpdatesAsync(MilvaionDbContext dbContext,
                                                         Dictionary<Guid, (bool isFailed, string exception)> jobUpdates,
                                                         CancellationToken cancellationToken)
    {
        try
        {
            var jobIds = jobUpdates.Keys.ToList();

            // Fetch jobs that need circuit breaker updates
            var jobs = await dbContext.ScheduledJobs
                                      .AsNoTracking()
                                      .Where(j => jobIds.Contains(j.Id))
                                      .Select(ScheduledJob.Projections.CircuitBreaker)
                                      .ToListAsync(cancellationToken);

            if (jobs.Count == 0)
                return;

            var jobsToUpdate = new List<ScheduledJob>();
            var autoDisabledJobs = new List<ScheduledJob>();
            var failureWindowThreshold = DateTime.UtcNow.AddMinutes(-_autoDisableOptions.FailureWindowMinutes);

            foreach (var job in jobs)
            {
                var (isFailed, exception) = jobUpdates[job.Id];

                // Check if auto-disable is enabled for this job
                var isAutoDisableEnabled = job.AutoDisableSettings.Enabled ?? true; // Use job-specific or default to true

                if (!isAutoDisableEnabled)
                {
                    // Job explicitly disabled auto-disable, just track failures but don't disable
                    if (isFailed)
                    {
                        job.AutoDisableSettings.ConsecutiveFailureCount++;
                        job.AutoDisableSettings.LastFailureTime = DateTime.UtcNow;
                        jobsToUpdate.Add(job);
                    }
                    else
                    {
                        // Reset on success
                        if (job.AutoDisableSettings.ConsecutiveFailureCount > 0)
                        {
                            job.AutoDisableSettings.ConsecutiveFailureCount = 0;
                            job.AutoDisableSettings.LastFailureTime = null;
                            jobsToUpdate.Add(job);
                        }
                    }

                    continue;
                }

                if (isFailed)
                {
                    // Check if last failure is within the window, otherwise reset counter
                    if (job.AutoDisableSettings.LastFailureTime.HasValue && job.AutoDisableSettings.LastFailureTime.Value < failureWindowThreshold)
                    {
                        // Old failures, reset counter
                        job.AutoDisableSettings.ConsecutiveFailureCount = 1;
                    }
                    else
                    {
                        job.AutoDisableSettings.ConsecutiveFailureCount++;
                    }

                    job.AutoDisableSettings.LastFailureTime = DateTime.UtcNow;

                    // Get threshold (job-specific or global)
                    var threshold = job.AutoDisableSettings.Threshold ?? _autoDisableOptions.ConsecutiveFailureThreshold;

                    // Check if threshold reached
                    if (job.AutoDisableSettings.ConsecutiveFailureCount >= threshold && job.IsActive)
                    {
                        job.IsActive = false;
                        job.AutoDisableSettings.DisabledAt = DateTime.UtcNow;
                        job.AutoDisableSettings.DisableReason = $"Auto-disabled after {job.AutoDisableSettings.ConsecutiveFailureCount} consecutive failures. Last error: {TruncateException(exception, 200)}";

                        autoDisabledJobs.Add(job);

                        _logger.Warning("Job {JobId} ({JobName}) auto-disabled after {FailureCount} consecutive failures", job.Id, job.DisplayName ?? job.JobNameInWorker, job.AutoDisableSettings.ConsecutiveFailureCount);
                    }

                    jobsToUpdate.Add(job);
                }
                else
                {
                    // Success - reset failure counter
                    if (job.AutoDisableSettings.ConsecutiveFailureCount > 0 || job.AutoDisableSettings.DisabledAt.HasValue)
                    {
                        _logger.Debug("Job {JobId} completed successfully, resetting failure counter (was {Count})", job.Id, job.AutoDisableSettings.ConsecutiveFailureCount);

                        job.AutoDisableSettings.ConsecutiveFailureCount = 0;
                        job.AutoDisableSettings.LastFailureTime = null;

                        // Don't clear AutoDisabledAt - keep for history
                        jobsToUpdate.Add(job);
                    }
                }
            }

            // Bulk update all jobs
            if (jobsToUpdate.Count > 0)
            {
                await dbContext.BulkUpdateAsync(jobsToUpdate, (bc) =>
                {
                    bc.PropertiesToInclude = bc.PropertiesToIncludeOnUpdate = _jobCircuitBreakerUpdateProps;
                }, cancellationToken: cancellationToken);

                _logger.Debug("Updated circuit breaker state for {Count} jobs", jobsToUpdate.Count);
            }

            // Remove auto-disabled jobs from Redis scheduler (BATCH operation)
            if (autoDisabledJobs.Count > 0)
            {
                try
                {
                    var autoDisabledJobIds = autoDisabledJobs.Select(j => j.Id).ToList();

                    // Batch remove from scheduled set (single ZREM call)
                    await _redisScheduler.RemoveFromScheduledSetBulkAsync(autoDisabledJobIds, cancellationToken);

                    // Batch remove from cache (single pipeline)
                    await _redisScheduler.RemoveCachedJobsBulkAsync(autoDisabledJobIds, cancellationToken);

                    _logger.Debug("Removed {Count} auto-disabled jobs from Redis scheduler in batch", autoDisabledJobs.Count);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to remove auto-disabled jobs from Redis (non-critical)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process circuit breaker updates (non-critical)");
        }
    }

    private static string TruncateException(string exception, int maxLength)
    {
        if (string.IsNullOrEmpty(exception))
            return "Unknown error";

        return exception.Length <= maxLength ? exception : exception[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Safe ACK operation that checks channel state before acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    private async Task SafeAckAsync(ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || _channel == null || _channel.IsClosed)
            {
                _logger.Debug("Skipping ACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            _logger.Debug("Channel already closed during ACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to ACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
    }

    /// <summary>
    /// Safe NACK operation that checks channel state before negative acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    private async Task SafeNackAsync(ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || _channel == null || _channel.IsClosed)
            {
                _logger.Debug("Skipping NACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await _channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            _logger.Debug("Channel already closed during NACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to NACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
    }

    /// <summary>
    /// Stops the background service and cleans up resources.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("StatusTrackerService stopping...");

        try
        {
            //  Process remaining status updates
            await ProcessBatchAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process remaining batch during shutdown");
        }

        // Dispose semaphore
        _batchLock?.Dispose();

        try
        {
            if (_channel != null && !_channel.IsClosed)
            {
                await _channel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing RabbitMQ channel");
        }
        finally
        {
            _channel?.Dispose();
        }

        try
        {
            if (_connection != null && _connection.IsOpen)
            {
                await _connection.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing RabbitMQ connection");
        }
        finally
        {
            _connection?.Dispose();
        }

        await base.StopAsync(cancellationToken);
        _logger.Information("StatusTrackerService stopped");
    }

    /// <summary>
    /// Updates consumer-level job counters in Redis (batch operation with Lua script).
    /// Groups updates by workerId/instanceId/jobType and applies net changes in SINGLE Redis call.
    /// Example: 100 updates with net change calculation → 1 Lua script call
    /// </summary>
    private async Task UpdateConsumerCountersBatchAsync(List<(string workerId, string instanceId, string jobType, JobOccurrenceStatus previousStatus, JobOccurrenceStatus newStatus)> updates, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var redisWorkerService = scope.ServiceProvider.GetService<IRedisWorkerService>();

            if (redisWorkerService == null)
                return;

            // Group by workerId:instanceId:jobType and calculate net change
            // Key format: "workerId:instanceId:jobType" for instance-level tracking
            var netChanges = new Dictionary<string, int>();

            foreach (var (workerId, instanceId, jobType, previousStatus, newStatus) in updates)
            {
                // Use workerId:instanceId:jobType for instance-level job tracking
                var key = $"{workerId}:{instanceId}:{jobType}";

                // Increment when job starts running
                if (previousStatus != JobOccurrenceStatus.Running && newStatus == JobOccurrenceStatus.Running)
                {
                    netChanges.TryGetValue(key, out var currentChange);
                    netChanges[key] = currentChange + 1;
                }
                // Decrement when job finishes
                else if (previousStatus == JobOccurrenceStatus.Running && newStatus.IsFinalStatus())
                {
                    netChanges.TryGetValue(key, out var currentChange);
                    netChanges[key] = currentChange - 1;
                }
            }

            if (netChanges.Count == 0)
                return;

            await redisWorkerService.BatchUpdateConsumerJobCountsAsync(netChanges, cancellationToken);

            _logger.Debug("Batch updated {Count} consumer counters (from {TotalUpdates} status changes)", netChanges.Count, updates.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to update consumer counters (non-critical)");
        }
    }
}