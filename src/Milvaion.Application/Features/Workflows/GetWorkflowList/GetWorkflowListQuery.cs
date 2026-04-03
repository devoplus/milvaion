using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;

namespace Milvaion.Application.Features.Workflows.GetWorkflowList;

/// <summary>
/// Query to get the list of workflows.
/// </summary>
public record GetWorkflowListQuery : ListRequest, IListRequestQuery<WorkflowListDto>
{
    /// <summary>
    /// Search term to filter workflows.
    /// </summary>
    public string SearchTerm { get; set; }
}
