using Milvasoft.Milvaion.Sdk.Domain.Enums;

namespace Milvasoft.Milvaion.Sdk.Domain.JsonModels;

/// <summary>
/// Message model for worker log entries sent to producer via RabbitMQ.
/// Published to worker_logs_queue.
/// </summary>
public class WorkerLogMessage
{
    /// <summary>
    /// Correlation ID linking this log to a specific job occurrence.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Worker identifier that generated this log.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Log entry details.
    /// </summary>
    public OccurrenceLog Log { get; set; }

    /// <summary>
    /// Timestamp when this message was created (UTC).
    /// </summary>
    public DateTime MessageTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Batch message model for multiple worker logs sent in a single RabbitMQ message.
/// Used for high-throughput scenarios to reduce message overhead.
/// </summary>
public class WorkerLogBatchMessage
{
    /// <summary>
    /// Array of log messages in this batch.
    /// </summary>
    public List<WorkerLogMessage> Logs { get; set; } = [];

    /// <summary>
    /// Timestamp when this batch was created (UTC).
    /// </summary>
    public DateTime BatchTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of logs in this batch.
    /// </summary>
    public int Count => Logs?.Count ?? 0;
}

/// <summary>
/// Message model for job status updates sent from worker to producer via RabbitMQ.
/// Published to job_status_updates_queue.
/// </summary>
public class JobStatusUpdateMessage
{
    /// <summary>
    /// Correlation ID for the job occurrence.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Job ID (parent ScheduledJob).
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Worker identifier (e.g., "test-worker"). Used for worker-level tracking.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Instance identifier (e.g., "test-worker-bf06453b"). Used for instance-level job counts.
    /// </summary>
    public string InstanceId { get; set; }

    /// <summary>
    /// New status of the job occurrence.
    /// </summary>
    public JobOccurrenceStatus Status { get; set; }

    /// <summary>
    /// Start time (if status is Running).
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time (if status is Completed/Failed/Cancelled).
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Execution duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Result message (for Completed status).
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// Exception details (for Failed status).
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Timestamp when this message was created (UTC).
    /// </summary>
    public DateTime MessageTimestamp { get; set; } = DateTime.UtcNow;
}
