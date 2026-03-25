namespace Milvaion.Application.Dtos;

/// <summary>
/// Event data for job occurrence created event.
/// </summary>
public record OccurrenceCreatedSignal
{
    /// <summary>
    /// Id of a job occurrence.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Id of a job.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Name of the job occurrence.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// Create time of the job occurrence.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Start time of the job occurrence.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time of the job occurrence.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Worker id of the job occurrence.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Status of the job occurrence.
    /// </summary>
    public int Status { get; set; }
}

/// <summary>
/// Event data for job occurrence updated event.
/// </summary>
public record OccurrenceUpdatedSignal
{
    /// <summary>
    /// Id of a job occurrence.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Status of the job occurrence.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Worker id of the job occurrence.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Start time of the job occurrence.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time of the job occurrence.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration in milliseconds (only set on completion/failure).
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Exception message if the occurrence failed.
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// Workflow step status (only set for workflow step occurrences).
    /// </summary>
    public int? StepStatus { get; set; }
}

/// <summary>
/// Event data for workflow run status updates.
/// </summary>
public record WorkflowRunUpdatedSignal
{
    /// <summary>
    /// Workflow run ID.
    /// </summary>
    public Guid RunId { get; set; }

    /// <summary>
    /// Workflow ID.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Current status.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Step run states.
    /// </summary>
    public List<WorkflowStepRunSignal> StepRuns { get; set; } = [];
}

/// <summary>
/// Event data for a single workflow step run status.
/// </summary>
public record WorkflowStepRunSignal
{
    /// <summary>
    /// Step run ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Step definition ID.
    /// </summary>
    public Guid WorkflowStepId { get; set; }

    /// <summary>
    /// Step status.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Occurrence ID.
    /// </summary>
    public Guid? OccurrenceId { get; set; }
}
