using Microsoft.Extensions.Logging;
using Quartz;

namespace SampleQuartzWorker.Jobs;

/// <summary>
/// Sample Quartz job that logs a message periodically.
/// This job is automatically reported to Milvaion for monitoring.
/// </summary>
[DisallowConcurrentExecution]
public class SampleLogJob(ILogger<SampleLogJob> logger) : IJob
{
    private readonly ILogger<SampleLogJob> _logger = logger;

    public Task Execute(IJobExecutionContext context)
    {
        var fireTime = context.FireTimeUtc.LocalDateTime;
        var jobKey = context.JobDetail.Key;

        _logger.LogInformation("?? SampleLogJob executed at {FireTime}. JobKey: {JobKey}", fireTime, jobKey);

        // Simulate some work
        Thread.Sleep(100);

        _logger.LogInformation("? SampleLogJob completed successfully");

        return Task.CompletedTask;
    }
}
