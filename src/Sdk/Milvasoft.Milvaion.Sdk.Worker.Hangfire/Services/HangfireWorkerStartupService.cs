using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Extensions;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Milvasoft.Milvaion.Sdk.Worker.Utils;

namespace Milvasoft.Milvaion.Sdk.Worker.Hangfire.Services;

/// <summary>
/// Startup service that scans recurring jobs from Hangfire storage, registers them with Milvaion,
/// and starts WorkerListenerPublisher for heartbeats.
/// </summary>
public class HangfireWorkerStartupService(IOptions<WorkerOptions> workerOptions,
                                          ExternalJobRegistry jobRegistry,
                                          IExternalJobPublisher publisher,
                                          IServiceProvider serviceProvider,
                                          ILoggerFactory loggerFactory) : IHostedService
{
    private readonly WorkerOptions _workerOptions = workerOptions?.Value;
    private readonly MilvaionExternalSchedulerOptions _options = workerOptions?.Value?.ExternalScheduler;
    private readonly ExternalJobRegistry _jobRegistry = jobRegistry;
    private readonly IExternalJobPublisher _publisher = publisher;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<HangfireWorkerStartupService>();
    private WorkerListenerPublisher _workerListenerPublisher;

    private const int _maxRetries = 10;
    private const int _retryDelayMs = 2000;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger?.Information("HangfireWorkerStartupService starting...");

            // Scan recurring jobs from Hangfire storage and register them
            var jobConfigs = await ScanAndRegisterRecurringJobsAsync(cancellationToken);

            if (_workerOptions == null || _serviceProvider == null)
            {
                _logger?.Warning("WorkerOptions or ServiceProvider is null. WorkerListenerPublisher will not start.");
                return;
            }

            _logger?.Information("Starting WorkerListenerPublisher with {Count} Hangfire jobs...", jobConfigs.Count);

            _workerListenerPublisher = new WorkerListenerPublisher(Microsoft.Extensions.Options.Options.Create(_workerOptions),
                                                                   _logger,
                                                                   _serviceProvider,
                                                                   jobConfigs);

            await _workerListenerPublisher.StartAsync(cancellationToken);

            _logger?.Information("WorkerListenerPublisher started successfully");
        }
        catch (OperationCanceledException)
        {
            _logger?.Information("HangfireWorkerStartupService startup cancelled");
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Failed to start HangfireWorkerStartupService");
        }
    }

    /// <summary>
    /// Scans recurring jobs from Hangfire storage using IStorageConnection and registers them.
    /// </summary>
    private async Task<Dictionary<string, JobConsumerConfig>> ScanAndRegisterRecurringJobsAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Wait for Hangfire storage to be ready
                if (JobStorage.Current == null)
                {
                    _logger?.Debug("Hangfire storage not ready, retrying... (attempt {Attempt}/{MaxRetries})", attempt, _maxRetries);
                    await Task.Delay(_retryDelayMs, cancellationToken);
                    continue;
                }

                using var connection = JobStorage.Current.GetConnection();
                var recurringJobs = connection.GetRecurringJobs();

                if (recurringJobs == null || recurringJobs.Count == 0)
                {
                    if (attempt < _maxRetries)
                    {
                        _logger?.Debug("No recurring jobs found in storage, retrying... (attempt {Attempt}/{MaxRetries})", attempt, _maxRetries);
                        await Task.Delay(_retryDelayMs, cancellationToken);
                        continue;
                    }
                }
                else
                {
                    _logger?.Information("Found {Count} recurring jobs in Hangfire storage", recurringJobs.Count);

                    foreach (var recurringJob in recurringJobs)
                    {
                        await RegisterRecurringJobAsync(recurringJob, cancellationToken);
                    }

                    return _jobRegistry?.GetJobConfigs() ?? [];
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Error scanning recurring jobs (attempt {Attempt}/{MaxRetries})", attempt, _maxRetries);

                if (attempt < _maxRetries)
                    await Task.Delay(_retryDelayMs, cancellationToken);
            }
        }

        _logger?.Warning("No recurring jobs found after {MaxRetries} attempts. WorkerListenerPublisher will start with empty job list.", _maxRetries);
        return [];
    }

    /// <summary>
    /// Registers a single recurring job with local registry and publishes to Milvaion.
    /// </summary>
    private async Task RegisterRecurringJobAsync(RecurringJobDto recurringJob, CancellationToken cancellationToken)
    {
        try
        {
            var jobType = recurringJob.Job?.Type;
            var methodName = recurringJob.Job?.Method?.Name ?? "Execute";

            if (jobType == null)
            {
                _logger?.Debug("Skipping recurring job {JobId} - job type is null", recurringJob.Id);
                return;
            }

            var externalJobId = jobType.GetExternalJobId(methodName);

            // Register in local registry
            _jobRegistry?.RegisterJob(externalJobId, jobType);

            // Publish registration to Milvaion
            if (_publisher != null && _options != null)
            {
                var message = new ExternalJobRegistrationMessage
                {
                    ExternalJobId = externalJobId,
                    Source = _options.Source,
                    DisplayName = recurringJob.Id ?? $"{jobType.Name}.{methodName}",
                    Description = $"Hangfire recurring job: {recurringJob.Id}",
                    JobTypeName = jobType.FullName ?? jobType.Name,
                    CronExpression = recurringJob.Cron,
                    NextExecuteAt = recurringJob.NextExecution,
                    WorkerId = _workerOptions?.WorkerId,
                    IsActive = true
                };

                await _publisher.PublishJobRegistrationAsync(message, cancellationToken);
                _logger?.Debug("Registered Hangfire recurring job: {JobId} ({ExternalJobId})", recurringJob.Id, externalJobId);
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Failed to register recurring job: {JobId}", recurringJob.Id);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger?.Information("HangfireWorkerStartupService stopping...");

            if (_workerListenerPublisher != null)
            {
                await _workerListenerPublisher.StopAsync(cancellationToken);
                _logger?.Information("WorkerListenerPublisher stopped successfully");
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error stopping HangfireWorkerStartupService");
        }
    }
}
