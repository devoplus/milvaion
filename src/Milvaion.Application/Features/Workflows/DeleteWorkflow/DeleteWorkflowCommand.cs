using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.Workflows.DeleteWorkflow;

/// <summary>
/// Command to delete a workflow.
/// </summary>
public record DeleteWorkflowCommand : ICommand<Guid>
{
    /// <summary>
    /// Workflow ID to delete.
    /// </summary>
    public Guid WorkflowId { get; set; }
}
