using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunDetail;

/// <summary>
/// Query to get a workflow run detail with step run states.
/// </summary>
public record GetWorkflowRunDetailQuery : IQuery<WorkflowRunDetailDto>
{
    /// <summary>
    /// Workflow run ID.
    /// </summary>
    public Guid RunId { get; set; }
}
