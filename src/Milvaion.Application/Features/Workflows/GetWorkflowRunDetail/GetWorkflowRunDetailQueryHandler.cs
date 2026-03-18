using Milvaion.Application.Dtos.WorkflowDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunDetail;

/// <summary>
/// Handles the workflow run detail query.
/// </summary>
public class GetWorkflowRunDetailQueryHandler(IMilvaionRepositoryBase<WorkflowRun> runRepository,
                                               IMilvaionRepositoryBase<JobOccurrence> occurrenceRepository,
                                               IMilvaionRepositoryBase<WorkflowStep> stepRepository,
                                               IMilvaionRepositoryBase<Workflow> workflowRepository,
                                               IMilvaionRepositoryBase<ScheduledJob> jobRepository) : IInterceptable, IQueryHandler<GetWorkflowRunDetailQuery, WorkflowRunDetailDto>
{
    private readonly IMilvaionRepositoryBase<WorkflowRun> _runRepository = runRepository;
    private readonly IMilvaionRepositoryBase<JobOccurrence> _occurrenceRepository = occurrenceRepository;
    private readonly IMilvaionRepositoryBase<WorkflowStep> _stepRepository = stepRepository;
    private readonly IMilvaionRepositoryBase<Workflow> _workflowRepository = workflowRepository;
    private readonly IMilvaionRepositoryBase<ScheduledJob> _jobRepository = jobRepository;

    /// <inheritdoc/>
    public async Task<Response<WorkflowRunDetailDto>> Handle(GetWorkflowRunDetailQuery request, CancellationToken cancellationToken)
    {
        var run = await _runRepository.GetByIdAsync(request.RunId, cancellationToken: cancellationToken);

        if (run == null)
            return Response<WorkflowRunDetailDto>.Success(null, "Workflow run not found.");

        var workflow = await _workflowRepository.GetByIdAsync(run.WorkflowId, cancellationToken: cancellationToken);
        var stepOccurrences = await _occurrenceRepository.GetAllAsync(
            condition: o => o.WorkflowRunId == run.Id,
            projection: o => o,
            cancellationToken: cancellationToken);

        // Get step definitions and job names
        var stepDtos = new List<WorkflowStepRunDto>();

        foreach (var occ in stepOccurrences ?? [])
        {
            var step = occ.WorkflowStepId.HasValue
                ? await _stepRepository.GetByIdAsync(occ.WorkflowStepId.Value, cancellationToken: cancellationToken)
                : null;

            string jobDisplayName = "Unknown";

            if (step != null)
            {
                var job = await _jobRepository.GetByIdAsync(step.JobId, cancellationToken: cancellationToken);
                jobDisplayName = job?.DisplayName ?? "Unknown";
            }

            stepDtos.Add(new WorkflowStepRunDto
            {
                Id = occ.Id,
                WorkflowStepId = occ.WorkflowStepId ?? Guid.Empty,
                StepName = step?.StepName,
                JobId = step?.JobId ?? Guid.Empty,
                JobDisplayName = jobDisplayName,
                OccurrenceId = occ.Id,
                Status = occ.StepStatus ?? WorkflowStepStatus.Pending,
                OutputData = occ.Result,
                Error = occ.Exception,
                StartTime = occ.StartTime,
                EndTime = occ.EndTime,
                DurationMs = occ.DurationMs,
                RetryCount = occ.StepRetryCount,
                DependsOnStepIds = step?.DependsOnStepIds,
                Condition = step?.Condition,
                DelaySeconds = step?.DelaySeconds ?? 0,
                Order = step?.Order ?? 0,
                PositionX = step?.PositionX,
                PositionY = step?.PositionY,
            });
        }

        stepDtos.Sort((a, b) => a.Order.CompareTo(b.Order));

        var dto = new WorkflowRunDetailDto
        {
            Id = run.Id,
            WorkflowId = run.WorkflowId,
            WorkflowName = workflow?.Name,
            WorkflowVersion = run.WorkflowVersion,
            CorrelationId = run.CorrelationId,
            Status = run.Status,
            StartTime = run.StartTime,
            EndTime = run.EndTime,
            DurationMs = run.DurationMs,
            TriggerReason = run.TriggerReason,
            Error = run.Error,
            StepRuns = stepDtos
        };

        return Response<WorkflowRunDetailDto>.Success(dto);
    }
}
