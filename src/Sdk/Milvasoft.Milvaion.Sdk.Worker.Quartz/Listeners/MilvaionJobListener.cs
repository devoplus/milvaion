using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Quartz;

namespace Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;

/// <summary>
/// Quartz job listener that intercepts job executions and reports them to Milvaion.
/// All methods are wrapped in try-catch to ensure Milvaion integration never affects Quartz operation.
/// </summary>
public class MilvaionJobListener(IExternalJobPublisher publisher, IOptions<WorkerOptions> workerOptions, ILoggerFactory loggerFactory) : IJobListener
{
    private readonly IExternalJobPublisher _publisher = publisher;
    private readonly MilvaionExternalSchedulerOptions _options = workerOptions.Value.ExternalScheduler;
    private readonly WorkerOptions _workerOptions = workerOptions?.Value;
    private readonly IMilvaLogger _logger = loggerFactory?.CreateMilvaLogger<MilvaionJobListener>();

    public string Name => "MilvaionJobListener";

    /// <summary>
    /// Called before a job is executed. Creates a new occurrence in Milvaion.
    /// </summary>
    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_publisher == null || _options == null)
                return;

            var correlationId = Guid.CreateVersion7();
            var fireInstanceId = context.FireInstanceId;

            // Store correlation ID in JobDataMap so job can access it for logging
            context.MergedJobDataMap.Put("Milvaion_CorrelationId", correlationId.ToString());
            context.MergedJobDataMap.Put("Milvaion_WorkerId", _workerOptions?.WorkerId ?? "unknown");

            var message = new ExternalJobOccurrenceMessage
            {
                CorrelationId = correlationId,
                ExternalJobId = context.JobDetail.Key.GetExternalJobId(),
                ExternalOccurrenceId = fireInstanceId,
                Source = _options.Source,
                JobTypeName = context.JobDetail.JobType.FullName ?? context.JobDetail.JobType.Name,
                EventType = ExternalOccurrenceEventType.Starting,
                WorkerId = _workerOptions?.WorkerId,
                Status = JobOccurrenceStatus.Running,
                ScheduledFireTime = context.ScheduledFireTimeUtc?.UtcDateTime,
                ActualFireTime = context.FireTimeUtc.UtcDateTime,
                StartTime = DateTime.UtcNow
            };

            await _publisher.PublishOccurrenceEventAsync(message, cancellationToken);

            _logger?.Debug("Job starting: {JobKey}, CorrelationId: {CorrelationId}", context.JobDetail.Key, correlationId);
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobToBeExecuted", context?.JobDetail?.Key.ToString());
        }
    }

    /// <summary>
    /// Called after a job has been executed. Updates the occurrence with final status.
    /// </summary>
    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_publisher == null || _options == null)
                return;

            var endTime = DateTime.UtcNow;
            var status = jobException == null ? JobOccurrenceStatus.Completed : JobOccurrenceStatus.Failed;
            var correlationIdString = context.MergedJobDataMap.GetString("Milvaion_CorrelationId");
            var correlationId = !string.IsNullOrEmpty(correlationIdString) && Guid.TryParse(correlationIdString, out var parsedGuid) ? parsedGuid : Guid.CreateVersion7();

            var message = new ExternalJobOccurrenceMessage
            {
                CorrelationId = correlationId,
                ExternalJobId = context.JobDetail.Key.GetExternalJobId(),
                Source = _options.Source,
                JobTypeName = context.JobDetail.JobType.FullName ?? context.JobDetail.JobType.Name,
                EventType = ExternalOccurrenceEventType.Completed,
                WorkerId = _workerOptions?.WorkerId,
                Status = status,
                ScheduledFireTime = context.ScheduledFireTimeUtc?.UtcDateTime,
                ActualFireTime = context.FireTimeUtc.UtcDateTime,
                EndTime = endTime,
                DurationMs = (long)context.JobRunTime.TotalMilliseconds,
                Result = jobException == null ? $"Job {context.JobDetail.Key} completed successfully" : null,
                Exception = jobException?.ToString()
            };

            await _publisher.PublishOccurrenceEventAsync(message, cancellationToken);

            _logger?.Debug("Job completed: {JobKey}, Status: {Status}, Duration: {Duration}ms, CorrelationId: {CorrelationId}", context.JobDetail.Key, status, context.JobRunTime.TotalMilliseconds, correlationId);
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobWasExecuted", context?.JobDetail?.Key.ToString());
        }
    }

    /// <summary>
    /// Called when a job execution is vetoed by a trigger listener.
    /// </summary>
    public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_publisher == null || _options == null)
                return;

            var correlationId = Guid.CreateVersion7();

            var message = new ExternalJobOccurrenceMessage
            {
                CorrelationId = correlationId,
                ExternalJobId = context.JobDetail.Key.GetExternalJobId(),
                Source = _options.Source,
                JobTypeName = context.JobDetail.JobType.FullName ?? context.JobDetail.JobType.Name,
                EventType = ExternalOccurrenceEventType.Vetoed,
                WorkerId = _workerOptions?.WorkerId,
                Status = JobOccurrenceStatus.Cancelled,
                ScheduledFireTime = context.ScheduledFireTimeUtc?.UtcDateTime,
                ActualFireTime = context.FireTimeUtc.UtcDateTime,
                EndTime = DateTime.UtcNow,
                Result = "Job execution was vetoed"
            };

            await _publisher.PublishOccurrenceEventAsync(message, cancellationToken);

            _logger?.Debug("Job vetoed: {JobKey}, CorrelationId: {CorrelationId}", context.JobDetail.Key, correlationId);
        }
        catch (Exception ex)
        {
            LogSafeError(ex, "JobExecutionVetoed", context?.JobDetail?.Key.ToString());
        }
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
