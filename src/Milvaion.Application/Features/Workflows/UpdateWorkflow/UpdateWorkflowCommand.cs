using Milvaion.Application.Features.Workflows.CreateWorkflow;
using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.Workflows.UpdateWorkflow;

/// <summary>
/// Command to update an existing workflow's settings.
/// </summary>
public record UpdateWorkflowCommand : ICommand<Guid>
{
    /// <summary>
    /// ID of the workflow to update.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Display name of the workflow.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the workflow.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Whether this workflow is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Failure handling strategy.
    /// </summary>
    public WorkflowFailureStrategy FailureStrategy { get; set; }

    /// <summary>
    /// Maximum retries for failed steps.
    /// </summary>
    public int MaxStepRetries { get; set; }

    /// <summary>
    /// Timeout in seconds for entire workflow.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Cron expression for automatic recurring execution (6-part format: second minute hour day month dayOfWeek).
    /// Null or empty means manual-only trigger.
    /// </summary>
    public string CronExpression { get; set; }

    /// <summary>
    /// Steps to update in this workflow. When provided, replaces all existing steps.
    /// TempId can be an existing step's real GUID (preserved) or a temporary string (new step gets a new GUID).
    /// If null, steps are not modified.
    /// </summary>
    public List<CreateWorkflowStepDto> Steps { get; set; }
}
