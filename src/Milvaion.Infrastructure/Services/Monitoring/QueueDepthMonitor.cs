using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AdminDtos;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Utils.Enums;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Utils;

namespace Milvaion.Infrastructure.Services.Monitoring;

/// <summary>
/// Implementation of queue depth monitoring service.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="QueueDepthMonitor"/> class.
/// </remarks>
public class QueueDepthMonitor(RabbitMQConnectionFactory connectionFactory, IOptions<RabbitMQOptions> options, ILoggerFactory loggerFactory, RabbitMQManagementClient managementClient) : IQueueDepthMonitor
{
    private readonly RabbitMQConnectionFactory _connectionFactory = connectionFactory;
    private readonly RabbitMQOptions _options = options.Value;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<QueueDepthMonitor>();
    private readonly RabbitMQManagementClient _managementClient = managementClient;

    /// <inheritdoc/>
    public async Task<QueueDepthInfo> GetQueueDepthAsync(string queueName, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var channel = await _connectionFactory.CreateChannelAsync(cancellationToken);

            // Use QueueDeclare with passive=true instead of QueueDeclarePassive
            var queueInfo = await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);

            var healthStatus = DetermineHealthStatus(queueInfo.MessageCount);

            var healthMessage = GetHealthMessage(queueInfo.MessageCount, healthStatus);

            return new QueueDepthInfo
            {
                QueueName = queueName,
                MessageCount = queueInfo.MessageCount,
                ConsumerCount = queueInfo.ConsumerCount,
                HealthStatus = healthStatus,
                HealthMessage = healthMessage
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get queue depth for {QueueName}", queueName);

            return new QueueDepthInfo
            {
                QueueName = queueName,
                MessageCount = 0,
                ConsumerCount = 0,
                HealthStatus = QueueHealthStatus.Unavailable,
                HealthMessage = $"Queue unavailable: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsQueueHealthyAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var info = await GetQueueDepthAsync(queueName, cancellationToken);

        if (info.HealthStatus == QueueHealthStatus.Critical)
        {
            _logger.Error("Queue {QueueName} at CRITICAL level: {MessageCount} messages", queueName, info.MessageCount);
            return false;
        }

        if (info.HealthStatus == QueueHealthStatus.Warning)
        {
            _logger.Warning("Queue {QueueName} at WARNING level: {MessageCount} messages", queueName, info.MessageCount);
        }

        return info.HealthStatus != QueueHealthStatus.Unavailable;
    }

    /// <inheritdoc/>
    public async Task<QueueStats> GetDetailedStatsAsync(string queueName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try Management API first for richer stats (MessagesUnacknowledged, MessagesReady)
            var managementInfo = await _managementClient.GetQueueAsync(queueName, cancellationToken);

            if (managementInfo != null)
            {
                var mgmtHealthStatus = DetermineHealthStatus(managementInfo.Messages);

                return new QueueStats
                {
                    QueueName = queueName,
                    MessageCount = managementInfo.Messages,
                    ConsumerCount = managementInfo.Consumers,
                    MessagesReady = managementInfo.MessagesReady,
                    MessagesUnacknowledged = managementInfo.MessagesUnacknowledged,
                    HealthStatus = mgmtHealthStatus,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Fallback to AMQP passive declare when Management API is unavailable
            await using var channel = await _connectionFactory.CreateChannelAsync(cancellationToken);
            var queueInfo = await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);

            var healthStatus = DetermineHealthStatus(queueInfo.MessageCount);

            return new QueueStats
            {
                QueueName = queueName,
                MessageCount = queueInfo.MessageCount,
                ConsumerCount = queueInfo.ConsumerCount,
                MessagesReady = queueInfo.MessageCount,
                MessagesUnacknowledged = 0,
                HealthStatus = healthStatus,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get detailed stats for {QueueName}", queueName);

            return new QueueStats
            {
                QueueName = queueName,
                MessageCount = 0,
                ConsumerCount = 0,
                MessagesReady = 0,
                MessagesUnacknowledged = 0,
                HealthStatus = QueueHealthStatus.Unavailable,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <inheritdoc/>
    public async Task<List<QueueStats>> GetAllQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        // Use the Management API to discover all queues in the vhost (including dynamic
        // consumer routing-key queues such as scheduled_jobs_queue.SampleWorker that are
        // created automatically when workers bind to the exchange).
        // Falls back to the known core queue list when the Management API is unavailable.
        var managementQueues = await _managementClient.GetAllQueuesAsync(cancellationToken);

        if (managementQueues.Count > 0)
        {
            var mgmtResults = managementQueues.Select(q =>
            {
                var healthStatus = DetermineHealthStatus(q.Messages);

                return new QueueStats
                {
                    QueueName = q.Name,
                    MessageCount = q.Messages,
                    ConsumerCount = q.Consumers,
                    MessagesReady = q.MessagesReady,
                    MessagesUnacknowledged = q.MessagesUnacknowledged,
                    HealthStatus = healthStatus,
                    Timestamp = DateTime.UtcNow
                };
            });

            return [.. mgmtResults];
        }

        // Fallback: monitor the known core queues via AMQP passive declare
        var queueNames = new[]
        {
            WorkerConstant.Queues.Jobs,
            WorkerConstant.Queues.WorkerLogs,
            WorkerConstant.Queues.WorkerHeartbeat,
            WorkerConstant.Queues.WorkerRegistration,
            WorkerConstant.Queues.StatusUpdates,
            WorkerConstant.Queues.FailedOccurrences,
            WorkerConstant.Queues.ExternalJobRegistration,
            WorkerConstant.Queues.ExternalJobOccurrence,
        };

        var tasks = queueNames.Select(queueName => GetDetailedStatsAsync(queueName, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return [.. results];
    }

    private QueueHealthStatus DetermineHealthStatus(uint messageCount)
    {
        if (messageCount >= _options.QueueDepthCriticalThreshold)
            return QueueHealthStatus.Critical;

        if (messageCount >= _options.QueueDepthWarningThreshold)
            return QueueHealthStatus.Warning;

        return QueueHealthStatus.Healthy;
    }

    private string GetHealthMessage(uint messageCount, QueueHealthStatus status) => status switch
    {
        QueueHealthStatus.Healthy => "Queue operating normally",
        QueueHealthStatus.Warning => $"Queue depth elevated: {messageCount}/{_options.QueueDepthWarningThreshold} (warning threshold)",
        QueueHealthStatus.Critical => $"Queue at critical capacity: {messageCount}/{_options.QueueDepthCriticalThreshold} (critical threshold)",
        QueueHealthStatus.Unavailable => "Queue is unavailable",
        _ => "Unknown status"
    };
}
