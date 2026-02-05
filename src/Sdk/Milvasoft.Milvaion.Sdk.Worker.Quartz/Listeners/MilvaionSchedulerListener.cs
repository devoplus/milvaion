using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using Quartz;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;

/// <summary>
/// Quartz scheduler listener that registers jobs with Milvaion when they are scheduled.
/// Also populates ExternalJobRegistry and starts WorkerListenerPublisher for heartbeats.
/// All methods are wrapped in try-catch to ensure Milvaion integration never affects Quartz operation.
/// </summary>
public class MilvaionSchedulerListener(IExternalJobPublisher publisher,
                                       IOptions<WorkerOptions> workerOptions,
                                       ExternalJobRegistry jobRegistry,
                                       IServiceProvider serviceProvider,
                                       ILoggerFactory loggerFactory) : ISchedulerListener
{
    private readonly IExternalJobPublisher _publisher = publisher;
    private readonly MilvaionExternalSchedulerOptions _options = workerOptions.Value.ExternalScheduler;
    private readonly WorkerOptions _workerOptions = workerOptions?.Value;
    private readonly ExternalJobRegistry _jobRegistry = jobRegistry;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IMilvaLogger _logger = loggerFactory?.CreateMilvaLogger<MilvaionSchedulerListener>();
    private WorkerListenerPublisher _workerListenerPublisher;

    // Track job details for combining with trigger info (JobKey -> JobDetail)
    private readonly ConcurrentDictionary<string, IJobDetail> _pendingJobDetails = new();

    /// <summary>
    /// Called when a job is scheduled. Combines job detail with trigger and publishes registration.
    /// </summary>
    public async Task JobScheduled(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Job scheduled with trigger: {TriggerKey} for job {JobKey}", trigger.Key, trigger.JobKey);

            // Try to get the pending job detail
            var jobKeyString = $"{trigger.JobKey.Group}.{trigger.JobKey.Name}";

            if (_pendingJobDetails.TryRemove(jobKeyString, out var jobDetail))
            {
                // Get cron expression from trigger
                string cronExpression = null;

                if (trigger is ICronTrigger cronTrigger)
                    cronExpression = cronTrigger.CronExpressionString;

                // Update registry
                _jobRegistry?.RegisterJob(jobDetail.Key.GetExternalJobId(), jobDetail.JobType);

                // Publish registration message with trigger info
                if (_publisher != null)
                {
                    var message = CreateJobRegistrationMessage(jobDetail, trigger, trigger.GetNextFireTimeUtc());

                    await _publisher.PublishJobRegistrationAsync(message, cancellationToken);

                    _logger?.Information("Registered job with Milvaion (with trigger): {JobKey}, Cron: {Cron}", jobDetail.Key, cronExpression ?? "N/A");
                }
            }
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobScheduled", trigger?.JobKey?.ToString());
        }
    }

    /// <summary>
    /// Called when a job is added to the scheduler.
    /// Stores job detail for later combination with trigger in JobScheduled.
    /// </summary>
    public Task JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = default)
    {
        try
        {
            var jobKeyString = $"{jobDetail.Key.Group}.{jobDetail.Key.Name}";

            // Store job detail for later use when trigger is added
            _pendingJobDetails[jobKeyString] = jobDetail;

            _logger?.Debug("Job added (awaiting trigger): {JobKey}", jobDetail.Key);
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobAdded", jobDetail?.Key.ToString());
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a job is deleted from the scheduler.
    /// </summary>
    public async Task JobDeleted(JobKey jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_publisher == null || _options == null)
                return;

            var message = new ExternalJobRegistrationMessage
            {
                ExternalJobId = jobKey.GetExternalJobId(),
                Source = _options.Source,
                DisplayName = jobKey.Name,
                JobTypeName = "Unknown",
                WorkerId = _workerOptions?.WorkerId,
                IsActive = false
            };

            await _publisher.PublishJobRegistrationAsync(message, cancellationToken);

            _logger?.Information("Marked job as inactive in Milvaion: {JobKey}", jobKey);
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobDeleted", jobKey.ToString());
        }
    }

    /// <summary>
    /// Called when the scheduler is starting.
    /// </summary>
    public Task SchedulerStarting(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Information("Quartz scheduler starting, Milvaion integration active");
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "SchedulerStarting");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the scheduler has started. Starts WorkerListenerPublisher with registered jobs.
    /// </summary>
    public async Task SchedulerStarted(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Information("Quartz scheduler started");

            var jobConfigs = _jobRegistry?.GetJobConfigs();

            if (jobConfigs == null || jobConfigs.Count == 0)
            {
                _logger?.Warning("No Quartz jobs found in registry. WorkerListenerPublisher will not start.");
                return;
            }

            if (_workerOptions == null || _serviceProvider == null)
            {
                _logger?.Warning("WorkerOptions or ServiceProvider is null. WorkerListenerPublisher will not start.");
                return;
            }

            _logger?.Information("Starting WorkerListenerPublisher with {Count} Quartz jobs...", jobConfigs.Count);

            _workerListenerPublisher = new WorkerListenerPublisher(Microsoft.Extensions.Options.Options.Create(_workerOptions),
                                                                   _logger,
                                                                   _serviceProvider,
                                                                   jobConfigs);

            await _workerListenerPublisher.StartAsync(cancellationToken);

            _logger?.Debug("WorkerListenerPublisher started successfully");
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "SchedulerStarted");
        }
    }

    /// <summary>
    /// Called when the scheduler is shutting down.
    /// </summary>
    public async Task SchedulerShuttingdown(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Information("Quartz scheduler shutting down");

            if (_workerListenerPublisher != null)
            {
                await _workerListenerPublisher.StopAsync(cancellationToken);

                _logger?.Debug("WorkerListenerPublisher stopped successfully");
            }
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "SchedulerShuttingdown");
        }
    }

    /// <summary>
    /// Called when the scheduler has shut down.
    /// </summary>
    public Task SchedulerShutdown(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Information("Quartz scheduler shut down");

            _workerListenerPublisher = null;
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "SchedulerShutdown");
        }

        return Task.CompletedTask;
    }

    public Task SchedulerInStandbyMode(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Scheduler in standby mode");
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task SchedulingDataCleared(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Scheduling data cleared");
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Error(cause, "Scheduler error: {Message}", msg);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Warning("Job interrupted: {JobKey}", jobKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Job unscheduled: {TriggerKey}", triggerKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Trigger finalized: {TriggerKey}", trigger.Key);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Trigger paused: {TriggerKey}", triggerKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task TriggersPaused(string triggerGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Triggers paused in group: {TriggerGroup}", triggerGroup);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Trigger resumed: {TriggerKey}", triggerKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task TriggersResumed(string triggerGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Triggers resumed in group: {TriggerGroup}", triggerGroup);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobPaused(JobKey jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Job paused: {JobKey}", jobKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobsPaused(string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Jobs paused in group: {JobGroup}", jobGroup);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobResumed(JobKey jobKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Job resumed: {JobKey}", jobKey);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    public Task JobsResumed(string jobGroup, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.Debug("Jobs resumed in group: {JobGroup}", jobGroup);
        }
        catch { /* Ignore */ }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a job registration message from a Quartz job detail.
    /// </summary>
    private ExternalJobRegistrationMessage CreateJobRegistrationMessage(IJobDetail jobDetail, ITrigger trigger, DateTimeOffset? nextFireTime)
    {
        string cronExpression = GetCronExpression(trigger);

        string jobData = null;

        if (jobDetail.JobDataMap?.Count > 0)
        {
            try
            {
                var dataDict = jobDetail.JobDataMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                jobData = JsonSerializer.Serialize(dataDict);
            }
            catch
            {
                _logger.Warning("Failed to serialize job data");
            }
        }

        return new ExternalJobRegistrationMessage
        {
            ExternalJobId = jobDetail.Key.GetExternalJobId(),
            Source = _options?.Source ?? "Quartz",
            DisplayName = jobDetail.Key.Name,
            Description = jobDetail.Description,
            JobTypeName = jobDetail.JobType.FullName ?? jobDetail.JobType.Name,
            Tags = jobDetail.Key.Group != JobKey.DefaultGroup ? jobDetail.Key.Group : null,
            CronExpression = cronExpression,
            NextExecuteAt = nextFireTime?.UtcDateTime,
            JobData = jobData,
            WorkerId = _workerOptions?.WorkerId,
            IsActive = true
        };
    }

    private string GetCronExpression(ITrigger trigger)
    {
        string cronExpression = null;
        int? intervalSeconds;

        // Extract schedule info based on trigger type
        if (trigger is ICronTrigger cronTrigger)
        {
            cronExpression = cronTrigger.CronExpressionString;
        }
        else if (trigger is ISimpleTrigger simpleTrigger)
        {
            // Convert TimeSpan interval to seconds
            intervalSeconds = (int)simpleTrigger.RepeatInterval.TotalSeconds;
            int? repeatCount = simpleTrigger.RepeatCount;

            // Convert interval to cron for Milvaion compatibility
            cronExpression = MilvaionSdkExtensions.IntervalToCron(intervalSeconds.Value);

            _logger?.Debug("SimpleTrigger detected: Interval={IntervalSeconds}s, RepeatCount={RepeatCount}, GeneratedCron={Cron}", intervalSeconds, repeatCount, cronExpression);
        }
        else if (trigger is ICalendarIntervalTrigger calendarTrigger)
        {
            // Calendar interval trigger (e.g., every 2 hours, every 3 days)
            var interval = calendarTrigger.RepeatInterval;
            var unit = calendarTrigger.RepeatIntervalUnit;

            intervalSeconds = unit switch
            {
                IntervalUnit.Second => interval,
                IntervalUnit.Minute => interval * 60,
                IntervalUnit.Hour => interval * 3600,
                IntervalUnit.Day => interval * 86400,
                IntervalUnit.Week => interval * 604800,
                IntervalUnit.Month => interval * 2592000, // Approximate
                IntervalUnit.Year => interval * 31536000, // Approximate
                _ => interval * 60 // Default to minutes
            };

            cronExpression = MilvaionSdkExtensions.IntervalToCron(intervalSeconds.Value);

            _logger?.Debug("CalendarIntervalTrigger detected: Interval={Interval} {Unit}, GeneratedCron={Cron}", interval, unit, cronExpression);
        }
        else if (trigger is IDailyTimeIntervalTrigger dailyTrigger)
        {
            // Daily time interval trigger
            var interval = dailyTrigger.RepeatInterval;
            var unit = dailyTrigger.RepeatIntervalUnit;

            intervalSeconds = unit switch
            {
                IntervalUnit.Second => interval,
                IntervalUnit.Minute => interval * 60,
                IntervalUnit.Hour => interval * 3600,
                _ => interval * 60
            };

            cronExpression = MilvaionSdkExtensions.IntervalToCron(intervalSeconds.Value);

            _logger?.Debug("DailyTimeIntervalTrigger detected: Interval={Interval} {Unit}, GeneratedCron={Cron}", interval, unit, cronExpression);
        }

        return cronExpression;
    }

    /// <summary>
    /// Safely logs an error without throwing.
    /// </summary>
    private void LogSafeError(Exception ex, string methodName, string context = null)
    {
        try
        {
            if (string.IsNullOrEmpty(context))
                _logger?.Error(ex, "[Milvaion] Error in {Method} - integration continues silently", methodName);
            else
                _logger?.Error(ex, "[Milvaion] Error in {Method} for {Context} - integration continues silently", methodName, context);
        }
        catch
        {
            // Even logging failed - silently continue
        }
    }
}
