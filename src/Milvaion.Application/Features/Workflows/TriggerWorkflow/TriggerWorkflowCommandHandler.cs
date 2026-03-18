using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Milvaion.Application.Features.Workflows.TriggerWorkflow;

/// <summary>
/// Handles workflow triggering. Creates a WorkflowRun with step runs for all steps.
/// The WorkflowEngine background service picks up Pending runs and dispatches root steps.
/// </summary>
[Log]
[UserActivityTrack(UserActivity.CreateScheduledJob)]
public record TriggerWorkflowCommandHandler(IMilvaionRepositoryBase<Workflow> WorkflowRepository,
                                             IMilvaionRepositoryBase<WorkflowRun> RunRepository) : IInterceptable, ICommandHandler<TriggerWorkflowCommand, Guid>
{
    private readonly IMilvaionRepositoryBase<Workflow> _workflowRepository = WorkflowRepository;
    private readonly IMilvaionRepositoryBase<WorkflowRun> _runRepository = RunRepository;

    /// <inheritdoc/>
    public async Task<Response<Guid>> Handle(TriggerWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, projection: Workflow.Projections.Trigger, cancellationToken: cancellationToken);

        if (workflow == null)
            return Response<Guid>.Error(default, "Workflow not found.");

        if (!workflow.IsActive)
            return Response<Guid>.Error(default, "Workflow is not active.");

        // Get steps for this workflow
        var steps = workflow.Steps;

        if (steps == null || steps.Count == 0)
            return Response<Guid>.Error(default, "Workflow has no steps.");

        var correlationId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        var workflowRun = new WorkflowRun
        {
            Id = Guid.CreateVersion7(),
            WorkflowId = workflow.Id,
            WorkflowVersion = workflow.Version,
            CorrelationId = correlationId,
            Status = WorkflowStatus.Pending,
            TriggerReason = request.Reason ?? "Manual trigger",
            CreatedAt = now,
        };

        // Pre-create one JobOccurrence per step in Pending state.
        // WorkflowEngineService picks these up and dispatches them when dependencies are satisfied.
        foreach (var step in steps.OrderBy(s => s.Order))
        {
            workflowRun.StepOccurrences.Add(new JobOccurrence
            {
                Id = Guid.CreateVersion7(),
                WorkflowRunId = workflowRun.Id,
                WorkflowStepId = step.Id,
                JobId = step.JobId,
                CorrelationId = correlationId,
                StepStatus = WorkflowStepStatus.Pending,
                Status = JobOccurrenceStatus.Queued,
                CreatedAt = now,
            });
        }

        await _runRepository.AddAsync(workflowRun, cancellationToken: cancellationToken);

        return Response<Guid>.Success(workflowRun.Id, "Workflow triggered successfully. The engine will start executing root steps.");
    }
}
