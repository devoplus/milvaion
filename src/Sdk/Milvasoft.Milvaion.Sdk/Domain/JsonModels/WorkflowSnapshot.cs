using Milvasoft.Milvaion.Sdk.Domain.Enums;

namespace Milvasoft.Milvaion.Sdk.Domain.JsonModels;

/// <summary>
/// Represents a snapshot of a workflow definition, including its properties and steps, used for serialization and transfer.
/// </summary>
public class WorkflowSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Tags { get; set; }
    public bool IsActive { get; set; }
    public WorkflowFailureStrategy FailureStrategy { get; set; }
    public int MaxStepRetries { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int Version { get; set; }
    public string CronExpression { get; set; }
    public DateTime? LastScheduledRunAt { get; set; }
    public DateTime? CreationDate { get; set; }
    public string CreatorUserName { get; set; }
    public DateTime? LastModificationDate { get; set; }
    public string LastModifierUserName { get; set; }
    public List<WorkflowStepSnapshot> Steps { get; set; }
}

/// <summary>
/// Represents a snapshot of a workflow definition, including its properties and steps, used for serialization and transfer.
/// </summary>
public class WorkflowStepSnapshot
{
    /// <summary>
    /// Id of step.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Parent workflow ID.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// The scheduled job this step executes.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Job version for this step (for reference, not necessarily unique). Used for visualization and debugging.
    /// </summary>
    public int JobVersion { get; set; }

    /// <summary>
    /// Job name for this step (for reference, not necessarily unique). Used for visualization and debugging.
    /// </summary>
    public string JobName { get; set; }

    /// <summary>
    /// User-friendly label for this step (e.g., "Extract Data", "Send Notification").
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Sort order / visual ordering hint for the step.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Comma-separated list of step IDs that must complete before this step can run.
    /// Empty or null means this is a root step (no dependencies).
    /// </summary>
    public string DependsOnStepIds { get; set; }

    /// <summary>
    /// JSONPath or expression to evaluate on the previous step's output.
    /// If the condition evaluates to false, this step is skipped.
    /// Null means always execute (unconditional).
    /// Example: "$.status == 'approved'" or "$.count > 0"
    /// </summary>
    public string Condition { get; set; }

    /// <summary>
    /// JSON mapping definition for passing data from parent steps to this step's job data.
    /// Format: { "sourceStepId:jsonPath": "targetJsonPath", ... }
    /// Null means use the job's default data.
    /// </summary>
    public string DataMappings { get; set; }

    /// <summary>
    /// Delay in seconds before executing this step after dependencies complete.
    /// 0 means execute immediately.
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

    /// <summary>
    /// Override job data for this step (JSON). If null, uses the job's default data.
    /// </summary>
    public string JobDataOverride { get; set; }

    /// <summary>
    /// X coordinate for DAG visualization.
    /// </summary>
    public double? PositionX { get; set; }

    /// <summary>
    /// Y coordinate for DAG visualization.
    /// </summary>
    public double? PositionY { get; set; }
}