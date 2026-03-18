using Milvaion.Application.Features.Workflows;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Milvaion.Application.Features.Workflows.CreateWorkflow;

/// <summary>
/// Handles workflow creation with DAG validation.
/// </summary>
[Log]
[UserActivityTrack(UserActivity.CreateScheduledJob)]
public record CreateWorkflowCommandHandler(IMilvaionRepositoryBase<Workflow> WorkflowRepository,
                                            IMilvaionRepositoryBase<WorkflowStep> StepRepository,
                                            IMilvaionRepositoryBase<ScheduledJob> JobRepository) : IInterceptable, ICommandHandler<CreateWorkflowCommand, Guid>
{
    private readonly IMilvaionRepositoryBase<Workflow> _workflowRepository = WorkflowRepository;
    private readonly IMilvaionRepositoryBase<WorkflowStep> _stepRepository = StepRepository;
    private readonly IMilvaionRepositoryBase<ScheduledJob> _jobRepository = JobRepository;

    /// <inheritdoc/>
    public async Task<Response<Guid>> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        if (request.Steps == null || request.Steps.Count == 0)
            return Response<Guid>.Error(default, "Workflow must have at least one step.");

        // Validate all referenced jobs exist
        var jobIds = request.Steps.Select(s => s.JobId).Distinct().ToList();
        var existingJobIds = new HashSet<Guid>();

        foreach (var jobId in jobIds)
        {
            var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken: cancellationToken);

            if (job != null)
                existingJobIds.Add(jobId);
        }

        var missingJobs = jobIds.Except(existingJobIds).ToList();

        if (missingJobs.Count > 0)
            return Response<Guid>.Error(default, $"Jobs not found: {string.Join(", ", missingJobs)}");

        // Build temp ID to real ID mapping
        var tempIdToRealId = new Dictionary<string, Guid>();

        foreach (var step in request.Steps)
        {
            tempIdToRealId[step.TempId ?? Guid.CreateVersion7().ToString()] = Guid.CreateVersion7();
        }

        // Validate DAG (no cycles)
        if (!request.Steps.ValidateDAG())
            return Response<Guid>.Error(default, "Workflow contains circular dependencies. Steps must form a Directed Acyclic Graph (DAG).");

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            Tags = request.Tags,
            IsActive = request.IsActive,
            FailureStrategy = request.FailureStrategy,
            MaxStepRetries = request.MaxStepRetries,
            TimeoutSeconds = request.TimeoutSeconds,
            CronExpression = request.CronExpression,
            Version = 1,
        };

        var steps = new List<WorkflowStep>();
        var tempIds = request.Steps.Select(s => s.TempId).ToList();

        for (int i = 0; i < request.Steps.Count; i++)
        {
            var stepCmd = request.Steps[i];
            var stepId = tempIdToRealId[stepCmd.TempId ?? tempIds[i] ?? Guid.CreateVersion7().ToString()];

            // Convert dependency temp IDs to real IDs
            string dependsOnStepIds = null;

            if (!string.IsNullOrWhiteSpace(stepCmd.DependsOnTempIds))
            {
                var depTempIds = stepCmd.DependsOnTempIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var realDepIds = depTempIds.Select(tid => tempIdToRealId.TryGetValue(tid, out var rid) ? rid.ToString() : null)
                                           .Where(id => id != null);
                dependsOnStepIds = string.Join(",", realDepIds);
            }

            steps.Add(new WorkflowStep
            {
                Id = stepId,
                WorkflowId = workflow.Id,
                JobId = stepCmd.JobId,
                StepName = stepCmd.StepName,
                Order = stepCmd.Order,
                DependsOnStepIds = dependsOnStepIds,
                Condition = stepCmd.Condition,
                DataMappings = stepCmd.DataMappings,
                DelaySeconds = stepCmd.DelaySeconds,
                JobDataOverride = ScheduledJob.FixJobData(stepCmd.JobDataOverride),
                PositionX = stepCmd.PositionX,
                PositionY = stepCmd.PositionY,
            });
        }

        workflow.Steps = steps;
        await _workflowRepository.AddAsync(workflow, cancellationToken: cancellationToken);

        return Response<Guid>.Success(workflow.Id, "Workflow created successfully.");
    }
}
