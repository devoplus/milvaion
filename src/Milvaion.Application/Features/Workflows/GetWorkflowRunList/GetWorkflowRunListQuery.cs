using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunList;

/// <summary>
/// Query to get workflow runs for a specific workflow.
/// </summary>
public record GetWorkflowRunListQuery : ListRequest, IListRequestQuery<WorkflowRunListDto>
{
    /// <summary>
    /// Workflow ID to filter runs for.
    /// </summary>
    public Guid? WorkflowId { get; set; }
}
