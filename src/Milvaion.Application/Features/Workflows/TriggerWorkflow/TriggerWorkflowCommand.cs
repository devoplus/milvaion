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

    /// <summary>
    /// Per-step job data overrides for this run.
    /// Key is the step ID (<see cref="WorkflowStepDefinition.Id"/>), value is JSON job data.
    /// Takes highest priority — overrides base data, design-time step override, and global job data.
    /// </summary>
    public Dictionary<Guid, string> StepJobData { get; set; }
}
