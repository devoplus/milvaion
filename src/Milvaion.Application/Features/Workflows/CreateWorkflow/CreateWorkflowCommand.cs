using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.Workflows.CreateWorkflow;

/// <summary>
/// Command to create a new workflow with its steps (DAG definition).
/// </summary>
public record CreateWorkflowCommand : ICommand<Guid>
{
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
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Failure handling strategy.
    /// </summary>
    public WorkflowFailureStrategy FailureStrategy { get; set; } = WorkflowFailureStrategy.StopOnFirstFailure;

    /// <summary>
    /// Maximum retries for failed steps.
    /// </summary>
    public int MaxStepRetries { get; set; } = 0;

    /// <summary>
    /// Timeout in seconds for entire workflow.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Cron expression for automatic recurring execution (6-part format: second minute hour day month dayOfWeek).
    /// Null means the workflow is only triggered manually.
    /// </summary>
    public string CronExpression { get; set; }

    /// <summary>
    /// Steps to include in this workflow.
    /// </summary>
    public List<CreateWorkflowStepDto> Steps { get; set; } = [];

    }

/// <summary>
/// DTO for creating a workflow step.
/// </summary>
public class CreateWorkflowStepDto
{
    /// <summary>
    /// Temporary client-side ID for referencing in DependsOnStepIds.
    /// </summary>
    public string TempId { get; set; }

    /// <summary>
    /// Job ID to execute.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Step display name.
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Sort order.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Comma-separated list of TempIds that must complete first.
    /// </summary>
    public string DependsOnTempIds { get; set; }

    /// <summary>
    /// Condition expression.
    /// </summary>
    public string Condition { get; set; }

    /// <summary>
    /// Data mapping definitions (JSON).
    /// </summary>
    public string DataMappings { get; set; }

    /// <summary>
    /// Delay in seconds.
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

    /// <summary>
    /// Override job data.
    /// </summary>
    public string JobDataOverride { get; set; }

    /// <summary>
    /// X position for DAG visualization.
    /// </summary>
    public double? PositionX { get; set; }

    /// <summary>
    /// Y position for DAG visualization.
    /// </summary>
    public double? PositionY { get; set; }
}
