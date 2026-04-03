using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.Workflows.TriggerWorkflow;

/// <summary>
/// Command to trigger a workflow run. Creates a WorkflowRun and dispatches root steps.
/// </summary>
public record TriggerWorkflowCommand : ICommand<Guid>
{
    /// <summary>
    /// Workflow ID to trigger.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Trigger reason.
    /// </summary>
    public string Reason { get; set; }
}
