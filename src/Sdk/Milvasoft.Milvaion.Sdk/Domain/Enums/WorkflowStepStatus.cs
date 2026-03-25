namespace Milvasoft.Milvaion.Sdk.Domain.Enums;

/// <summary>
/// Represents the status of a single workflow step run.
/// </summary>
public enum WorkflowStepStatus
{
    /// <summary>
    /// Step is waiting for its dependencies to complete.
    /// </summary>
    Pending,

    /// <summary>
    /// Step has been dispatched and is running.
    /// </summary>
    Running,

    /// <summary>
    /// Step completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Step execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Step was skipped due to a condition evaluation or upstream failure.
    /// </summary>
    Skipped,

    /// <summary>
    /// Step was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Step is waiting for a delayed execution time.
    /// </summary>
    Delayed,
}
