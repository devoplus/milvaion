namespace Milvasoft.Milvaion.Sdk.Domain.Enums;

/// <summary>
/// Defines the failure handling strategy for a workflow.
/// </summary>
public enum WorkflowFailureStrategy
{
    /// <summary>
    /// Stop the entire workflow on first step failure.
    /// </summary>
    StopOnFirstFailure,

    /// <summary>
    /// Continue executing independent branches even if one fails. Only dependent steps are skipped.
    /// </summary>
    ContinueOnFailure,
}
