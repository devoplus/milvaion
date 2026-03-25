using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using System.Linq.Expressions;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunList;

/// <summary>
/// Handles workflow run list query.
/// </summary>
public class GetWorkflowRunListQueryHandler(IMilvaionRepositoryBase<WorkflowRun> runRepository) : IInterceptable, IListQueryHandler<GetWorkflowRunListQuery, WorkflowRunListDto>
{
    private readonly IMilvaionRepositoryBase<WorkflowRun> _runRepository = runRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<WorkflowRunListDto>> Handle(GetWorkflowRunListQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<WorkflowRun, bool>> predicate = null;

        if (request.WorkflowId.HasValue)
            predicate = r => r.WorkflowId == request.WorkflowId.Value;

        var response = await _runRepository.GetAllAsync(request, condition: predicate, projection: WorkflowRunListDto.Projection, cancellationToken: cancellationToken);

        return response;
    }
}
