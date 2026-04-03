using Milvasoft.Milvaion.Sdk.Domain.Enums;

namespace Milvasoft.Milvaion.Sdk.Domain.JsonModels;

/// <summary>
/// Workflow definition containing steps and edges as JSONB.
/// Stored as a single JSON object in the Workflow table for atomic updates.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Workflow steps (DAG nodes).
    /// </summary>
    public List<WorkflowStepDefinition> Steps { get; set; } = [];

    /// <summary>
    /// Workflow edges (DAG connections).
    /// </summary>
    public List<WorkflowEdgeDefinition> Edges { get; set; } = [];
}

/// <summary>
/// Represents a single step (node) in a workflow DAG (DTO, not entity).
/// </summary>
public class WorkflowStepDefinition
{
    /// <summary>
    /// Step ID (GUID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Node type (Task, Condition, Merge).
    /// </summary>
    public WorkflowNodeType NodeType { get; set; } = WorkflowNodeType.Task;

    /// <summary>
    /// The scheduled job this step executes (required for Task nodes).
    /// </summary>
    public Guid? JobId { get; set; }

    /// <summary>
    /// User-friendly label for this step.
    /// </summary>
    public string StepName { get; set; }

    /// <summary>
    /// Sort order / visual ordering hint.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Node-specific configuration stored as JSON.
    /// For Condition nodes: { "expression": "@status == 'Completed'" }
    /// </summary>
    public string NodeConfigJson { get; set; }

    /// <summary>
    /// JSON mapping definition for passing data from parent steps.
    /// Format: { "sourceStepId:jsonPath": "targetJsonPath" }
    /// </summary>
    public string DataMappings { get; set; }

    /// <summary>
    /// Delay in seconds before executing this step.
    /// </summary>
    public int DelaySeconds { get; set; } = 0;

    /// <summary>
    /// Override job data for this step (JSON).
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

/// <summary>
/// Represents a directional connection between two workflow nodes (DTO, not entity).
/// </summary>
public class WorkflowEdgeDefinition
{
    /// <summary>
    /// Source node ID.
    /// </summary>
    public Guid SourceStepId { get; set; }

    /// <summary>
    /// Target node ID.
    /// </summary>
    public Guid TargetStepId { get; set; }

    /// <summary>
    /// Named output port on the source node (e.g., "true", "false" for Condition nodes).
    /// </summary>
    public string SourcePort { get; set; }

    /// <summary>
    /// Named input port on the target node.
    /// </summary>
    public string TargetPort { get; set; }

    /// <summary>
    /// Optional edge label shown in the UI.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Sort order / visual ordering hint.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Optional edge-specific configuration stored as JSON.
    /// </summary>
    public string EdgeConfigJson { get; set; }
}
