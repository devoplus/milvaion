using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Helpers;
using Milvasoft.Interception.Ef.Transaction;
using Milvasoft.Interception.Interceptors.Logging;

namespace Milvaion.Application.Features.Workflows.UpdateWorkflow;

/// <summary>
/// Handles workflow settings update.
/// </summary>
[Log]
[UserActivityTrack(UserActivity.UpdateScheduledJob)]
[Transaction]
public record UpdateWorkflowCommandHandler(IMilvaionRepositoryBase<Workflow> WorkflowRepository,
                                            IMilvaionRepositoryBase<WorkflowStep> StepRepository,
                                            IMilvaionRepositoryBase<WorkflowRun> RunRepository,
                                            IMilvaionRepositoryBase<JobOccurrence> JobOccurrenceRepository,
                                            IMilvaionRepositoryBase<ScheduledJob> JobRepository) : IInterceptable, ICommandHandler<UpdateWorkflowCommand, Guid>
{
    private readonly IMilvaionRepositoryBase<Workflow> _workflowRepository = WorkflowRepository;
    private readonly IMilvaionRepositoryBase<WorkflowStep> _stepRepository = StepRepository;
    private readonly IMilvaionRepositoryBase<WorkflowRun> _runRepository = RunRepository;
    private readonly IMilvaionRepositoryBase<JobOccurrence> _jobOccurrenceRepository = JobOccurrenceRepository;
    private readonly IMilvaionRepositoryBase<ScheduledJob> _jobRepository = JobRepository;

    /// <inheritdoc/>
    public async Task<Response<Guid>> Handle(UpdateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken: cancellationToken);

        if (workflow == null)
            return Response<Guid>.Error(default, "Workflow not found.");

        // Track if workflow definition changes (will trigger version snapshot)
        bool workflowDefinitionChanged = false;

        var cronChanged = workflow.CronExpression != request.CronExpression;

        WorkflowSnapshot workflowSnapshot = new()
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
            CreationDate = workflow.CreationDate,
            CreatorUserName = workflow.CreatorUserName,
            LastModificationDate = workflow.LastModificationDate,
            LastModifierUserName = workflow.LastModifierUserName,
            Steps = [],
        };

        if (workflow.Name != request.Name || workflow.IsActive != request.IsActive || workflow.FailureStrategy != request.FailureStrategy ||
            workflow.MaxStepRetries != request.MaxStepRetries || workflow.TimeoutSeconds != request.TimeoutSeconds || cronChanged)
        {
            workflowDefinitionChanged = true;
        }

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.Tags = request.Tags;
        workflow.IsActive = request.IsActive;
        workflow.FailureStrategy = request.FailureStrategy;
        workflow.MaxStepRetries = request.MaxStepRetries;
        workflow.TimeoutSeconds = request.TimeoutSeconds;
        workflow.CronExpression = string.IsNullOrWhiteSpace(request.CronExpression) ? null : request.CronExpression;
        workflow.Versions ??= [];

        // Reset last scheduled run time when cron expression changes so the engine picks up the new schedule immediately
        if (cronChanged)
            workflow.LastScheduledRunAt = null;

        if (request.Steps.Count == 0)
            return Response<Guid>.Error(default, "Workflow must have at least one step.");

        // Block step update while active runs are in progress
        var activeRuns = await _runRepository.GetAllAsync<WorkflowRun>(
            condition: r => r.WorkflowId == request.WorkflowId && (r.Status == WorkflowStatus.Pending || r.Status == WorkflowStatus.Running),
            projection: r => new() { Id = r.Id },
            conditionAfterProjection: null,
            tracking: false,
            splitQuery: false,
            cancellationToken: cancellationToken);

        if (!activeRuns.IsNullOrEmpty())
            return Response<Guid>.Error(default, "Cannot update steps while there are active workflow runs. Please wait for them to complete.");

        // Validate all referenced jobs exist
        var jobIds = request.Steps.Select(s => s.JobId).Distinct().ToList();
        var existingJobIds = new HashSet<Guid>();

        var jobs = await _jobRepository.GetAllAsync(j => jobIds.Contains(j.Id), cancellationToken: cancellationToken);

        foreach (var job in jobs)
        {
            if (job != null)
                existingJobIds.Add(job.Id);
        }

        var missingJobs = jobIds.Except(existingJobIds).ToList();

        if (missingJobs.Count > 0)
            return Response<Guid>.Error(default, $"Jobs not found: {string.Join(", ", missingJobs)}");

        // Validate DAG (no cycles)
        if (!request.Steps.ValidateDAG())
            return Response<Guid>.Error(default, "Workflow contains circular dependencies. Steps must form a Directed Acyclic Graph (DAG).");

        // Get existing steps
        var existingSteps = await _stepRepository.GetAllAsync(
            condition: s => s.WorkflowId == request.WorkflowId,
            projection: s => s,
            cancellationToken: cancellationToken) ?? [];

        var existingStepIdSet = existingSteps.Select(s => s.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build tempId → realId mapping: reuse existing IDs, generate new GUIDs for new steps
        var tempIdToRealId = new Dictionary<string, Guid>();

        for (int i = 0; i < request.Steps.Count; i++)
        {
            var stepCmd = request.Steps[i];
            var tempId = stepCmd.TempId ?? i.ToString();
            tempIdToRealId[tempId] = existingStepIdSet.Contains(tempId) ? Guid.Parse(tempId) : Guid.CreateVersion7();
        }

        // Classify and build step objects
        var stepsToAdd = new List<WorkflowStep>();
        var stepsToUpdate = new List<WorkflowStep>();

        for (int i = 0; i < request.Steps.Count; i++)
        {
            var stepCmd = request.Steps[i];
            var tempId = stepCmd.TempId ?? i.ToString();
            var stepId = tempIdToRealId[tempId];

            string dependsOnStepIds = null;

            if (!string.IsNullOrWhiteSpace(stepCmd.DependsOnTempIds))
            {
                var depTempIds = stepCmd.DependsOnTempIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var realDepIds = depTempIds.Select(tid => tempIdToRealId.TryGetValue(tid, out var rid) ? rid.ToString() : null).Where(id => id != null);
                dependsOnStepIds = string.Join(",", realDepIds);
            }

            var step = new WorkflowStep
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
            };

            if (existingStepIdSet.Contains(tempId))
                stepsToUpdate.Add(step);
            else
                stepsToAdd.Add(step);
        }

        // Delete only steps that were removed from the workflow
        var requestedStepIds = tempIdToRealId.Values.ToHashSet();
        var stepsToDelete = existingSteps.Where(s => !requestedStepIds.Contains(s.Id)).ToList();

        // Check if steps actually changed (not just resent)
        bool stepsActuallyChanged = stepsToAdd.Count > 0 || stepsToDelete.Count > 0;

        if (!stepsActuallyChanged && stepsToUpdate.Count > 0)
        {
            // Check if any updated step has different values
            var existingStepsDict = existingSteps.ToDictionary(s => s.Id);

            foreach (var updatedStep in stepsToUpdate)
            {
                if (!existingStepsDict.TryGetValue(updatedStep.Id, out var existingStep))
                    continue;

                if (existingStep.JobId != updatedStep.JobId ||
                    existingStep.StepName != updatedStep.StepName ||
                    existingStep.Order != updatedStep.Order ||
                    existingStep.DependsOnStepIds != updatedStep.DependsOnStepIds ||
                    existingStep.Condition != updatedStep.Condition ||
                    existingStep.DataMappings != updatedStep.DataMappings ||
                    existingStep.DelaySeconds != updatedStep.DelaySeconds ||
                    existingStep.JobDataOverride != updatedStep.JobDataOverride ||
                    existingStep.PositionX != updatedStep.PositionX ||
                    existingStep.PositionY != updatedStep.PositionY)
                {
                    stepsActuallyChanged = true;
                    break;
                }
            }
        }

        // Create version snapshot only if something actually changed
        if (workflowDefinitionChanged || stepsActuallyChanged)
        {
            // Create snapshot of current workflow with steps before any changes
            workflowSnapshot.Steps = existingSteps?.Select(s => new WorkflowStepSnapshot()
            {
                Id = s.Id,
                WorkflowId = s.WorkflowId,
                JobId = s.JobId,
                StepName = s.StepName,
                JobName = jobs.FirstOrDefault(j => j.Id == s.JobId)?.DisplayName,
                JobVersion = jobs.FirstOrDefault(j => j.Id == s.JobId)?.Version ?? 1,
                Order = s.Order,
                DependsOnStepIds = s.DependsOnStepIds,
                Condition = s.Condition,
                DataMappings = s.DataMappings,
                DelaySeconds = s.DelaySeconds,
                JobDataOverride = s.JobDataOverride,
                PositionX = s.PositionX,
                PositionY = s.PositionY
            }).ToList();

            workflow.Versions.Add(workflowSnapshot);
            workflow.Version++;
        }

        if (stepsToDelete.Count > 0)
        {
            await _jobOccurrenceRepository.ExecuteDeleteAsync(o => stepsToDelete.Select(s => s.Id).ToList().Contains(o.WorkflowStepId.Value), cancellationToken: cancellationToken);
            await _stepRepository.DeleteAsync(stepsToDelete, cancellationToken: cancellationToken);
        }

        if (stepsToUpdate.Count > 0)
            await _stepRepository.BulkUpdateAsync(stepsToUpdate, bc => bc.PropertiesToIncludeOnUpdate =
            [
                nameof(WorkflowStep.JobId), nameof(WorkflowStep.StepName), nameof(WorkflowStep.Order),
                nameof(WorkflowStep.DependsOnStepIds), nameof(WorkflowStep.Condition), nameof(WorkflowStep.DataMappings),
                nameof(WorkflowStep.DelaySeconds), nameof(WorkflowStep.JobDataOverride),
                nameof(WorkflowStep.PositionX), nameof(WorkflowStep.PositionY),
            ], cancellationToken: cancellationToken);

        if (stepsToAdd.Count > 0)
            await _stepRepository.BulkAddAsync(stepsToAdd, cancellationToken: cancellationToken);

        // Update workflow with version history and incremented version
        await _workflowRepository.UpdateAsync(workflow, cancellationToken: cancellationToken);

        return Response<Guid>.Success(workflow.Id, "Workflow updated successfully.");
    }
}
