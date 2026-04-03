namespace Milvasoft.Milvaion.Sdk.Domain.Enums;

/// <summary>
/// Represents the type of a workflow node.
/// </summary>
public enum WorkflowNodeType
{
    /// <summary>
    /// Standard task node that dispatches a scheduled job.
    /// </summary>
    Task,

    /// <summary>
    /// Decision node that evaluates an expression and routes flow through ports.
    /// </summary>
    Condition,

    /// <summary>
    /// Merge node that joins multiple branches.
    /// </summary>
    Merge
}
