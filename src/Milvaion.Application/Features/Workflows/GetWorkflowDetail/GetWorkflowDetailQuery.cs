using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;

namespace Milvaion.Application.Features.Workflows.GetWorkflowDetail;

/// <summary>
/// Query to get workflow detail.
/// </summary>
public record GetWorkflowDetailQuery : IQuery<WorkflowDetailDto>
{
    /// <summary>
    /// Workflow ID.
    /// </summary>
    public Guid WorkflowId { get; set; }
}
