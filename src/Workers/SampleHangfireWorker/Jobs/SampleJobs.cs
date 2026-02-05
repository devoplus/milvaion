using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using System.Text.Json;

namespace SampleHangfireWorker.Jobs;

/// <summary>
/// Sample Hangfire job that logs messages periodically.
/// </summary>
#pragma warning disable CA1873 // Avoid potentially expensive logging
public class SampleLogJob(ILogger<SampleLogJob> logger)
{
    private readonly ILogger<SampleLogJob> _logger = logger;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public async Task ExecuteAsync(PerformContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 SampleLogJob started at {Time}", DateTime.UtcNow);

        // Simulate work
        for (int i = 1; i <= 5; i++)
        {
            _logger.LogInformation("⏳ Processing step {Step}/5...", i);
            await Task.Delay(500, cancellationToken);
        }

        _logger.LogInformation("✅ SampleLogJob completed successfully at {Time}", DateTime.UtcNow);
    }
}

/// <summary>
/// Sample Hangfire job that simulates sending an email.
/// Demonstrates Milvaion log publishing with Hangfire.
/// </summary>
public class SendEmailJob(ILogger<SendEmailJob> logger, ILogPublisher logPublisher)
{
    private readonly ILogger<SendEmailJob> _logger = logger;
    private readonly ILogPublisher _logPublisher = logPublisher;

    public async Task ExecuteAsync(PerformContext context, string recipient, string subject, CancellationToken cancellationToken)
    {
        // Get Milvaion tracking info from job parameters (set by MilvaionJobFilter)
        var correlationIdStr = context.GetJobParameter<string>("Milvaion_CorrelationId");
        var workerId = context.GetJobParameter<string>("Milvaion_WorkerId") ?? "hangfire-worker";
        var correlationId = Guid.TryParse(correlationIdStr, out var cid) ? cid : Guid.Empty;

        _logger.LogInformation("📧 SendEmailJob started!");
        _logger.LogInformation("Sending email to: {Recipient}", recipient);
        _logger.LogInformation("Subject: {Subject}", subject);

        // Publish log to Milvaion
        await PublishLogAsync(correlationId, workerId, LogLevel.Information, $"Starting email send to {recipient}", new { Recipient = recipient, Subject = subject });

        // Simulate email sending with progress updates
        for (int i = 0; i <= 100; i += 20)
        {
            _logger.LogInformation("Sending progress: {Progress}%", i);

            // Publish progress log to Milvaion
            await PublishLogAsync(correlationId, workerId, LogLevel.Debug, $"Sending progress: {i}%", new { Progress = i });

            await Task.Delay(1000, cancellationToken);
        }

        // Simulate random failure for testing (10% chance)
        if (Random.Shared.Next(10) == 0)
        {
            await PublishLogAsync(correlationId, workerId, LogLevel.Error, "Email sending failed - simulated error", new { Error = "SimulatedFailure" });
            _logger.LogError("❌ Email sending failed - simulated error");
            throw new InvalidOperationException("Simulated email sending failure");
        }

        _logger.LogInformation("✅ Email sent successfully to {Recipient}", recipient);

        // Publish success log to Milvaion
        await PublishLogAsync(correlationId, workerId, LogLevel.Information, $"Email sent successfully to {recipient}", new { Recipient = recipient, SentAt = DateTime.UtcNow });

        // Flush logs before job completes
        await _logPublisher.FlushAsync(cancellationToken);
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