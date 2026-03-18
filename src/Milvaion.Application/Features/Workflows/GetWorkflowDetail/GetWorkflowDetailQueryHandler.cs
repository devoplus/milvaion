using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Features.Workflows.GetWorkflowDetail;

/// <summary>
/// Handles the workflow detail query.
/// </summary>
public class GetWorkflowDetailQueryHandler(IMilvaionRepositoryBase<Workflow> workflowRepository,
                                            IMilvaionRepositoryBase<ScheduledJob> jobRepository) : IInterceptable, IQueryHandler<GetWorkflowDetailQuery, WorkflowDetailDto>
{
    private readonly IMilvaionRepositoryBase<Workflow> _workflowRepository = workflowRepository;
    private readonly IMilvaionRepositoryBase<ScheduledJob> _jobRepository = jobRepository;

    /// <inheritdoc/>
    public async Task<Response<WorkflowDetailDto>> Handle(GetWorkflowDetailQuery request, CancellationToken cancellationToken)
    {
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, projection: Workflow.Projections.Detail, cancellationToken: cancellationToken);

        if (workflow == null)
            return Response<WorkflowDetailDto>.Success(null, "Workflow not found");

        // Get job names for steps
        var jobIds = workflow.Steps.Select(s => s.JobId).Distinct().ToList();
        var jobNames = new Dictionary<Guid, string>();

        var jobs = await _jobRepository.GetAllAsync(j => jobIds.Contains(j.Id), cancellationToken: cancellationToken);

        foreach (var jobId in jobIds)
        {
            var job = jobs.FirstOrDefault(j => j.Id == jobId);

            if (job != null)
                jobNames[jobId] = job.DisplayName;
        }

        var dto = new WorkflowDetailDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Tags = workflow.Tags,
            IsActive = workflow.IsActive,
            FailureStrategy = workflow.FailureStrategy,
            MaxStepRetries = workflow.MaxStepRetries,
            TimeoutSeconds = workflow.TimeoutSeconds,
            Version = workflow.Version,
            CronExpression = workflow.CronExpression,
            LastScheduledRunAt = workflow.LastScheduledRunAt,
            WorkflowVersions = workflow.Versions?.OrderByDescending(i => i.Version).ToList(),
            Steps = workflow.Steps?.Select(s => new WorkflowStepDto
            {
                Id = s.Id,
                JobId = s.JobId,
                JobDisplayName = jobNames.GetValueOrDefault(s.JobId, "Unknown"),
                StepName = s.StepName,
                Order = s.Order,
                DependsOnStepIds = s.DependsOnStepIds,
                Condition = s.Condition,
                DataMappings = s.DataMappings,
                DelaySeconds = s.DelaySeconds,
                JobDataOverride = s.JobDataOverride,
                PositionX = s.PositionX,
                PositionY = s.PositionY,
            }).OrderBy(s => s.Order).ToList()
        };

        return Response<WorkflowDetailDto>.Success(dto);
    }
}
