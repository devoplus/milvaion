namespace Milvaion.Domain.Enums;

/// <summary>
/// Defines the types of alerts that can be sent through the alerting system.
/// </summary>
public enum AlertType : byte
{
    /// <summary>
    /// Recieve all types of alerts.
    /// </summary>
    All = 0,

    /// <summary>
    /// Job dispatcher memory usage has reached critical levels.
    /// </summary>
    JobDispatcherMemoryUsageCritical = 1,

    /// <summary>
    /// Database connection failed or is unavailable.
    /// </summary>
    DatabaseConnectionFailed = 2,

    /// <summary>
    /// A zombie occurrence was detected (job stuck in running state).
    /// </summary>
    ZombieOccurrenceDetected = 3,

    /// <summary>
    /// A job was automatically disabled due to consecutive failures.
    /// </summary>
    JobAutoDisabled = 4,

    /// <summary>
    /// Queue depth has reached critical threshold.
    /// </summary>
    QueueDepthCritical = 5,

    /// <summary>
    /// A worker has disconnected unexpectedly.
    /// </summary>
    WorkerDisconnected = 6,

    /// <summary>
    /// Redis connection failed or is unavailable.
    /// </summary>
    RedisConnectionFailed = 7,

    /// <summary>
    /// RabbitMQ connection failed or is unavailable.
    /// </summary>
    RabbitMQConnectionFailed = 8,

    /// <summary>
    /// A scheduled job execution failed.
    /// </summary>
    JobExecutionFailed = 9,

    /// <summary>
    /// An unknown or unhandled exception occurred.
    /// </summary>
    UnknownException = 10,
}
