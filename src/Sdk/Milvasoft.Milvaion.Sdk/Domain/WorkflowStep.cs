using Milvasoft.Attributes.Annotations;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Milvasoft.Milvaion.Sdk.Domain;

/// <summary>
/// Represents a single step (node) in a workflow DAG.
/// Each step references a ScheduledJob and defines its edges (dependencies).
/// </summary>
[Table(SchedulerTableNames.WorkflowSteps)]
[DontIndexCreationDate]
public class WorkflowStep : CreationAuditableEntity<Guid>
{
    /// <summary>
    /// Parent workflow ID.
    /// </summary>
    [Required]
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// The scheduled job this step executes.
    /// </summary>
    [Required]
    public Guid JobId { get; set; }

    /// <summary>
    /// User-friendly label for this step (e.g., "Extract Data", "Send Notification").
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string StepName { get; set; }

    /// <summary>
    /// Sort order / visual ordering hint for the step.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Comma-separated list of step IDs that must complete before this step can run.
    /// Empty or null means this is a root step (no dependencies).
    /// </summary>
    [MaxLength(4000)]
    public string DependsOnStepIds { get; set; }

    /// <summary>
    /// JSONPath or expression to evaluate on the previous step's output.
    /// If the condition evaluates to false, this step is skipped.
    /// Null means always execute (unconditional).
    /// Example: "$.status == 'approved'" or "$.count > 0"
    /// </summary>
    [MaxLength(1000)]
    public string Condition { get; set; }

    /// <summary>
    /// JSON mapping definition for passing data from parent steps to this step's job data.
    /// Format: { "sourceStepId:jsonPath": "targetJsonPath", ... }
    /// Null means use the job's default data.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string DataMappings { get; set; }

    /// <summary>
    /// Delay in seconds before executing this step after dependencies complete.
    /// 0 means execute immediately.
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

    /// <summary>
    /// Override job data for this step (JSON). If null, uses the job's default data.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string JobDataOverride { get; set; }

    /// <summary>
    /// X coordinate for DAG visualization.
    /// </summary>
    public double? PositionX { get; set; }

    /// <summary>
    /// Y coordinate for DAG visualization.
    /// </summary>
    public double? PositionY { get; set; }

    /// <summary>
    /// Navigation to the parent workflow.
    /// </summary>
    [ForeignKey(nameof(WorkflowId))]
    public virtual Workflow Workflow { get; set; }

    /// <summary>
    /// Navigation to the referenced job.
    /// </summary>
    [ForeignKey(nameof(JobId))]
    public virtual ScheduledJob Job { get; set; }
}
