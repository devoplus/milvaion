using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Quartz;
using System.Text.Json;

namespace SampleQuartzWorker.Jobs;

/// <summary>
/// Sample Quartz job that simulates sending an email.
/// Demonstrates job data usage, error handling, and Milvaion log publishing.
/// </summary>
#pragma warning disable CA1873 // Avoid potentially expensive logging
public class SendEmailJob(ILogger<SendEmailJob> logger, ILogPublisher logPublisher) : IJob
{
    private readonly ILogger<SendEmailJob> _logger = logger;
    private readonly ILogPublisher _logPublisher = logPublisher;

    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;

        // Get Milvaion tracking info from JobDataMap (set by MilvaionJobListener)
        var correlationIdStr = dataMap.GetString("Milvaion_CorrelationId");
        var workerId = dataMap.GetString("Milvaion_WorkerId") ?? "quartz-worker";
        var correlationId = Guid.TryParse(correlationIdStr, out var cid) ? cid : Guid.Empty;

        var recipient = dataMap.GetString("Recipient") ?? "unknown@example.com";
        var subject = dataMap.GetString("Subject") ?? "No Subject";

        _logger.LogInformation("📧 Sending email to {Recipient} with subject: {Subject}", recipient, subject);

        // Publish log to Milvaion
        await PublishLogAsync(correlationId, workerId, LogLevel.Information, $"Starting email send to {recipient}", new { Recipient = recipient, Subject = subject });

        // Simulate email sending delay
        await Task.Delay(500);

        for (var i = 0; i < 30000; i += 1000)
        {
            // Publish progress log
            await PublishLogAsync(correlationId, workerId, LogLevel.Debug, "Sending email..." + i);

            // Simulate email sending
            await Task.Delay(1000, context.CancellationToken);
        }

        // Publish progress log
        await PublishLogAsync(correlationId, workerId, LogLevel.Debug, "Email prepared and ready to send", new { Step = "Prepared" });

        // Simulate random failure for testing
        if (Random.Shared.Next(10) == 0)
        {
            await PublishLogAsync(correlationId, workerId, LogLevel.Error, "Email sending failed - simulated error", new { Error = "SimulatedFailure" });
            throw new InvalidOperationException("Simulated email sending failure");
        }

        _logger.LogInformation("✅ Email sent successfully to {Recipient}", recipient);

        // Publish success log
        await PublishLogAsync(correlationId, workerId, LogLevel.Information, $"Email sent successfully to {recipient}", new { Recipient = recipient, SentAt = DateTime.UtcNow });

        // Flush logs before job completes
        await _logPublisher.FlushAsync();

        // Store result in job execution context
        context.Result = JsonSerializer.Serialize(new { Recipient = recipient, SentAt = DateTime.UtcNow });
    }

    private async Task PublishLogAsync(Guid correlationId, string workerId, LogLevel level, string message, object data = null)
    {
        if (correlationId == Guid.Empty)
            return;

        try
        {
            Dictionary<string, object> dataDict = null;

            if (data != null)
            {
                // Convert anonymous object to Dictionary
                var json = JsonSerializer.Serialize(data);
                dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }

            var log = new OccurrenceLog
            {
                Level = level.ToString(),
                Message = message,
                Timestamp = DateTime.UtcNow,
                Data = dataDict,
                Category = "UserCode"
            };

            await _logPublisher.PublishLogAsync(correlationId, workerId, log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish log to Milvaion");
        }
    }
}
#pragma warning restore CA1873 // Avoid potentially expensive logging
