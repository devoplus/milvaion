using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.Workflows.CancelWorkflow;

/// <summary>
/// Command to cancel a running workflow.
/// </summary>
public record CancelWorkflowCommand : ICommand<bool>
{
    /// <summary>
    /// The workflow run ID to cancel.
    /// </summary>
    public Guid WorkflowRunId { get; init; }

    /// <summary>
    /// Reason for cancellation.
    /// </summary>
    public string Reason { get; init; } = "Manual cancellation";
}
