namespace Milvasoft.Milvaion.Sdk.Domain.Enums;

/// <summary>
/// Represents the status of a workflow run.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// Workflow run is pending and has not started yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Workflow is currently executing steps.
    /// </summary>
    Running,

    /// <summary>
    /// All steps completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// One or more steps failed and the failure strategy halted the workflow.
    /// </summary>
    Failed,

    /// <summary>
    /// Workflow was cancelled by user or system.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Workflow completed with some steps skipped due to conditions.
    /// </summary>
    PartiallyCompleted,
}
