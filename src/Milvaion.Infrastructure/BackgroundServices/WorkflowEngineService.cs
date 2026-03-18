using Cronos;
using EFCore.BulkExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Features.Workflows.TriggerWorkflow;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Interfaces.RabbitMQ;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Infrastructure.BackgroundServices.Base;
using Milvaion.Infrastructure.Persistence.Context;
using Milvaion.Infrastructure.Telemetry;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Helpers;
using Milvasoft.Milvaion.Sdk.Utils;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Milvaion.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that orchestrates workflow (DAG) execution.
/// Polls for pending/running workflow runs and dispatches ready steps.
/// </summary>
public class WorkflowEngineService(IServiceProvider serviceProvider,
                                    IRabbitMQPublisher rabbitMQPublisher,
                                    IRedisSchedulerService redisScheduler,
                                    IRedisStatsService redisStatsService,
                                    IOptions<WorkflowEngineOptions> options,
                                    ILoggerFactory loggerFactory,
                                    BackgroundServiceMetrics metrics,
                                    IMemoryStatsRegistry memoryStatsRegistry = null) : MemoryTrackedBackgroundService(loggerFactory, options.Value, memoryStatsRegistry)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IRabbitMQPublisher _rabbitMQPublisher = rabbitMQPublisher;
    private readonly IRedisSchedulerService _redisScheduler = redisScheduler;
    private readonly IRedisStatsService _redisStatsService = redisStatsService;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<WorkflowEngineService>();
    private readonly WorkflowEngineOptions _options = options.Value;
    private readonly BackgroundServiceMetrics _metrics = metrics;

    private static readonly List<string> _workflowUpdateProps = [nameof(Workflow.LastScheduledRunAt)];
    private static readonly List<string> _workflowRunUpdateProps = [nameof(WorkflowRun.Status), nameof(WorkflowRun.StartTime), nameof(WorkflowRun.EndTime), nameof(WorkflowRun.DurationMs), nameof(WorkflowRun.Error)];
    private static readonly List<string> _stepOccurrenceUpdateProps = [nameof(JobOccurrence.StepStatus), nameof(JobOccurrence.StepRetryCount), nameof(JobOccurrence.StepScheduledAt), nameof(JobOccurrence.Exception), nameof(JobOccurrence.Result), nameof(JobOccurrence.Status), nameof(JobOccurrence.EndTime), nameof(JobOccurrence.DurationMs), nameof(JobOccurrence.JobName)];
    private static readonly List<string> _stepOccurrenceDispatchProps = [nameof(JobOccurrence.Status), nameof(JobOccurrence.StepStatus), nameof(JobOccurrence.JobName), nameof(JobOccurrence.ZombieTimeoutMinutes), nameof(JobOccurrence.JobVersion), nameof(JobOccurrence.ExecutionTimeoutSeconds)];
    private static readonly List<string> _stepOccurrenceFailProps = [nameof(JobOccurrence.Status), nameof(JobOccurrence.StepStatus), nameof(JobOccurrence.Exception)];

    /// <inheritdoc/>
    protected override string ServiceName => "WorkflowEngine";

    /// <inheritdoc/>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.Warning("Workflow engine is disabled. Skipping startup.");
            return;
        }

        _logger.Information("Workflow engine service starting...");

        // Wait for database to be ready
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MilvaionDbContext>();

                if (await dbContext.Database.CanConnectAsync(cancellationToken))
                    break;
            }
            catch
            {
                _logger.Information("Database not ready yet (attempt {Attempt}/30). Waiting 2s...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        await base.StartAsync(cancellationToken);
        _logger.Information("Workflow engine service started successfully.");
    }

    /// <inheritdoc/>
    protected override async Task ExecuteWithMemoryTrackingAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Workflow engine polling started. Interval: {Interval}s", _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckCronWorkflowsAsync(stoppingToken);
                await ProcessWorkflowRunsAsync(stoppingToken);
                TrackMemoryAfterIteration();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during workflow engine iteration");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Workflow engine service stopping...");
        await base.StopAsync(cancellationToken);
        _logger.Information("Workflow engine service stopped.");
    }

    /// <summary>
    /// Checks all active workflows with a cron expression and triggers a run if the schedule is due.
    /// </summary>
    private async Task CheckCronWorkflowsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MilvaionDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;

        var cronWorkflows = await dbContext.Workflows.Where(w => w.IsActive && w.CronExpression != null).ToListAsync(cancellationToken);

        var triggeredWorkflows = new List<Workflow>();

        foreach (var workflow in cronWorkflows)
        {
            try
            {
                var cronExpr = CronExpression.Parse(workflow.CronExpression, CronFormat.IncludeSeconds);

                // Use last scheduled run time as the base; fall back to slightly before now to catch first-ever runs
                var fromTime = workflow.LastScheduledRunAt ?? now.AddSeconds(-_options.PollingIntervalSeconds * 2);

                var nextOccurrence = cronExpr.GetNextOccurrence(fromTime, TimeZoneInfo.Utc);

                if (nextOccurrence.HasValue && nextOccurrence.Value <= now)
                {
                    var command = new TriggerWorkflowCommand { WorkflowId = workflow.Id, Reason = "Cron schedule" };
                    await mediator.Send(command, cancellationToken);

                    workflow.LastScheduledRunAt = now;
                    triggeredWorkflows.Add(workflow);

                    _logger.Information("Workflow {WorkflowId} triggered by cron schedule ({CronExpression})", workflow.Id, workflow.CronExpression);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking cron schedule for workflow {WorkflowId}", workflow.Id);
            }
        }

        if (triggeredWorkflows.Count > 0)
            await dbContext.BulkUpdateAsync(triggeredWorkflows, bc => bc.PropertiesToIncludeOnUpdate = _workflowUpdateProps, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Processes all active workflow runs.
    /// </summary>
    private async Task ProcessWorkflowRunsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MilvaionDbContext>();

        var activeRuns = await dbContext.WorkflowRuns
            .Include(r => r.StepOccurrences)
            .Include(r => r.Workflow).ThenInclude(w => w.Steps)
            .Where(r => r.Status == WorkflowStatus.Pending || r.Status == WorkflowStatus.Running)
            .ToListAsync(cancellationToken);

        if (activeRuns.IsNullOrEmpty())
            return;

        _logger.Debug("Processing {Count} active workflow runs", activeRuns.Count);

        var now = DateTime.UtcNow;

        // Phase 1: Process all runs in memory — collect all changes without touching the DB
        var runsToUpdate = new List<WorkflowRun>();
        var occurrencesToUpdate = new List<JobOccurrence>();
        var pendingDispatches = new List<(WorkflowRun Run, JobOccurrence Occurrence, WorkflowStep StepDef)>();
        var runningOccurrencesToCancel = new List<JobOccurrence>();

        foreach (var run in activeRuns)
        {
            try
            {
                CollectRunChanges(run, now, runsToUpdate, occurrencesToUpdate, pendingDispatches, runningOccurrencesToCancel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing workflow run {RunId}", run.Id);
            }
        }

        // Phase 2: Bulk load all jobs needed for dispatch and for naming non-dispatched occurrences (skipped, cancelled, delayed)
        var jobIds = pendingDispatches.Select(d => d.StepDef.JobId)
            .Concat(occurrencesToUpdate
                .Where(o => string.IsNullOrWhiteSpace(o.JobName) && o.WorkflowStepId.HasValue && o.JobId != Guid.Empty)
                .Select(o => o.JobId))
            .Distinct()
            .ToList();

        var jobsById = jobIds.Count > 0
            ? await dbContext.ScheduledJobs.Where(j => jobIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, cancellationToken)
            : [];

        // Populate JobName for occurrences that were never dispatched (skipped, cancelled, delayed)
        foreach (var occ in occurrencesToUpdate.Where(o => string.IsNullOrWhiteSpace(o.JobName) && o.WorkflowStepId.HasValue))
        {
            if (jobsById.TryGetValue(occ.JobId, out var nameJob))
                occ.JobName = nameJob.JobNameInWorker;
        }

        // Resolve job data for each dispatch; mark as failed if job not found
        var validDispatches = new List<(WorkflowRun Run, JobOccurrence Occurrence, WorkflowStep StepDef, ScheduledJob Job, string JobData)>();

        foreach (var (run, occurrence, stepDef) in pendingDispatches)
        {
            if (!jobsById.TryGetValue(stepDef.JobId, out var job))
            {
                occurrence.StepStatus = WorkflowStepStatus.Failed;
                occurrence.Exception = $"Job {stepDef.JobId} not found.";
                occurrencesToUpdate.Add(occurrence);
                continue;
            }

            var jobData = !string.IsNullOrWhiteSpace(stepDef.JobDataOverride) ? stepDef.JobDataOverride : job.JobData;
            var occurrencesDict = run.StepOccurrences.ToDictionary(o => o.WorkflowStepId!.Value);
            var stepDefsDict = run.Workflow?.Steps?.ToDictionary(s => s.Id) ?? [];

            if (!string.IsNullOrWhiteSpace(stepDef.DataMappings))
                jobData = ApplyDataMappings(jobData, stepDef.DataMappings, occurrencesDict, stepDefsDict);

            validDispatches.Add((run, occurrence, stepDef, job, jobData));
        }

        // Phase 3: Single bulk save for all run/step status changes
        if (runsToUpdate.Count > 0)
            await dbContext.BulkUpdateAsync(runsToUpdate, bc => bc.PropertiesToIncludeOnUpdate = _workflowRunUpdateProps, cancellationToken: cancellationToken);

        if (occurrencesToUpdate.Count > 0)
            await dbContext.BulkUpdateAsync(occurrencesToUpdate, bc => bc.PropertiesToIncludeOnUpdate = _stepOccurrenceUpdateProps, cancellationToken: cancellationToken);

        // Phase 4: Batch dispatch all ready steps
        if (validDispatches.Count > 0)
        {
            var eventPublisher = scope.ServiceProvider.GetService<IJobOccurrenceEventPublisher>();
            await BatchDispatchStepsAsync(dbContext, eventPublisher, validDispatches, cancellationToken);
        }

        // Phase 5: Send Redis cancellation signals for Running steps that were cancelled due to workflow failure/timeout
        if (runningOccurrencesToCancel.Count > 0)
        {
            var cancellationService = scope.ServiceProvider.GetService<IJobCancellationService>();

            if (cancellationService != null)
            {
                foreach (var occ in runningOccurrencesToCancel)
                {
                    try
                    {
                        await cancellationService.PublishCancellationAsync(occ.Id, occ.JobId, occ.Id, "Workflow stopped", cancellationToken);
                        await _redisScheduler.MarkJobAsCompletedAsync(occ.JobId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to send cancellation signal for workflow step occurrence {OccurrenceId}", occ.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes a single workflow run in memory: mutates run/step state and populates change lists.
    /// No database calls — all persistence is batched at the caller level.
    /// </summary>
    private void CollectRunChanges(
        WorkflowRun run,
        DateTime now,
        List<WorkflowRun> runsToUpdate,
        List<JobOccurrence> occurrencesToUpdate,
        List<(WorkflowRun Run, JobOccurrence Occurrence, WorkflowStep StepDef)> pendingDispatches,
        List<JobOccurrence> runningOccurrencesToCancel)
    {
        // Start pending run
        if (run.Status == WorkflowStatus.Pending)
        {
            run.Status = WorkflowStatus.Running;
            run.StartTime = now;
            runsToUpdate.Add(run);
        }

        // Check for timeout
        if (run.Workflow?.TimeoutSeconds > 0 && run.StartTime.HasValue &&
            (now - run.StartTime.Value).TotalSeconds > run.Workflow.TimeoutSeconds)
        {
            CancelRunInMemory(run, now, "Workflow timed out", runsToUpdate, occurrencesToUpdate, runningOccurrencesToCancel);
            return;
        }

        var stepDefinitions = run.Workflow?.Steps?.ToDictionary(s => s.Id) ?? [];
        var stepOccurrences = run.StepOccurrences.ToDictionary(o => o.WorkflowStepId!.Value);

        // Check for failures
        var failedOccs = run.StepOccurrences.Where(o => o.StepStatus == WorkflowStepStatus.Failed).ToList();

        if (failedOccs.Count > 0)
        {
            var failureStrategy = run.Workflow?.FailureStrategy ?? WorkflowFailureStrategy.StopOnFirstFailure;

            if (failureStrategy == WorkflowFailureStrategy.StopOnFirstFailure)
            {
                var maxRetries = run.Workflow?.MaxStepRetries ?? 0;
                var exhaustedOccs = failedOccs.Where(o => o.StepRetryCount >= maxRetries).ToList();

                if (exhaustedOccs.Count > 0 || maxRetries == 0)
                {
                    FailRunInMemory(run, now, $"Step(s) failed: {string.Join(", ", exhaustedOccs.Select(o => stepDefinitions.GetValueOrDefault(o.WorkflowStepId!.Value)?.StepName ?? o.WorkflowStepId.ToString()))}", runsToUpdate, occurrencesToUpdate, runningOccurrencesToCancel);
                    return;
                }

                var occsToRetry = failedOccs.Where(o => o.StepRetryCount < maxRetries).ToList();

                foreach (var occ in occsToRetry)
                {
                    occ.StepStatus = WorkflowStepStatus.Pending;
                    occ.StepRetryCount++;
                    occ.Exception = null;
                    occ.Result = null;
                    occ.Status = JobOccurrenceStatus.Queued;
                    occ.EndTime = null;
                    occ.DurationMs = null;
                }

                occurrencesToUpdate.AddRange(occsToRetry);
            }
        }

        // Find steps that are ready to execute
        foreach (var occ in run.StepOccurrences.Where(o => o.StepStatus == WorkflowStepStatus.Pending))
        {
            var stepDef = stepDefinitions.GetValueOrDefault(occ.WorkflowStepId!.Value);

            if (stepDef == null)
                continue;

            if (!AreDependenciesSatisfied(stepDef, stepOccurrences, run.Workflow?.FailureStrategy ?? WorkflowFailureStrategy.StopOnFirstFailure))
                continue;

            if (!string.IsNullOrWhiteSpace(stepDef.Condition) && !EvaluateCondition(stepDef, stepOccurrences))
            {
                occ.StepStatus = WorkflowStepStatus.Skipped;
                occ.Status = JobOccurrenceStatus.Skipped;
                occ.EndTime = now;
                occ.Exception = $"Step skipped: condition '{stepDef.Condition}' evaluated to false.";
                occurrencesToUpdate.Add(occ);
                continue;
            }

            if (stepDef.DelaySeconds > 0 && occ.StepScheduledAt == null)
            {
                occ.StepStatus = WorkflowStepStatus.Delayed;
                occ.StepScheduledAt = now.AddSeconds(stepDef.DelaySeconds);
                occurrencesToUpdate.Add(occ);
                continue;
            }

            pendingDispatches.Add((run, occ, stepDef));
        }

        // Also check delayed steps that are now due
        foreach (var occ in run.StepOccurrences.Where(o => o.StepStatus == WorkflowStepStatus.Delayed && o.StepScheduledAt <= now))
        {
            var stepDef = stepDefinitions.GetValueOrDefault(occ.WorkflowStepId!.Value);

            if (stepDef != null)
                pendingDispatches.Add((run, occ, stepDef));
        }

        // Check completion
        if (run.StepOccurrences.All(o => o.StepStatus is WorkflowStepStatus.Completed or WorkflowStepStatus.Failed or WorkflowStepStatus.Skipped or WorkflowStepStatus.Cancelled))
        {
            var hasFailures = run.StepOccurrences.Any(o => o.StepStatus == WorkflowStepStatus.Failed);
            var hasSkipped = run.StepOccurrences.Any(o => o.StepStatus == WorkflowStepStatus.Skipped);
            var hasCompleted = run.StepOccurrences.Any(o => o.StepStatus == WorkflowStepStatus.Completed);

            // Failures with no completions → Failed; failures alongside completions/skips → PartiallyCompleted
            run.Status = hasFailures && !hasCompleted
                ? WorkflowStatus.Failed
                : hasFailures || hasSkipped
                    ? WorkflowStatus.PartiallyCompleted
                    : WorkflowStatus.Completed;

            if (hasFailures)
                run.Error = "One or more steps failed.";

            run.EndTime = now;

            if (run.StartTime.HasValue)
                run.DurationMs = (long)(now - run.StartTime.Value).TotalMilliseconds;

            runsToUpdate.Add(run);

            _logger.Information("Workflow run {RunId} completed with status {Status}", run.Id, run.Status);
        }
    }

    /// <summary>
    /// Checks if all dependencies
    /// </summary>
    private static bool AreDependenciesSatisfied(WorkflowStep stepDef, Dictionary<Guid, JobOccurrence> stepOccurrences, WorkflowFailureStrategy failureStrategy)
    {
        if (string.IsNullOrWhiteSpace(stepDef.DependsOnStepIds))
            return true; // Root step

        var depIds = stepDef.DependsOnStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var depIdStr in depIds)
        {
            if (!Guid.TryParse(depIdStr, out var depStepId))
                continue;

            if (!stepOccurrences.TryGetValue(depStepId, out var depOcc))
                return false; // Dependency not found

            // Completed or skipped means satisfied
            if (depOcc.StepStatus is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped)
                continue;

            // For ContinueOnFailure strategy, Failed is also "satisfied" (allows downstream to run)
            if (failureStrategy == WorkflowFailureStrategy.ContinueOnFailure && depOcc.StepStatus == WorkflowStepStatus.Failed)
                continue;

            return false; // Dependency not yet satisfied
        }

        return true;
    }

    /// <summary>
    /// Evaluates a condition expression against parent step outputs and statuses.
    /// Supports:
    ///   - Logical operators: &amp;&amp; (AND), || (OR)  — AND has higher precedence
    ///   - @status == 'Completed'       → all parent(s) StepStatus check
    ///   - $.field == 'value'           → all parent(s) result JSON field check
    ///   - stepId:@status != 'Skipped'  → specific parent status check
    ///   - stepId:$.price > 100         → specific parent result field check
    /// </summary>
    private static bool EvaluateCondition(WorkflowStep stepDef, Dictionary<Guid, JobOccurrence> stepOccurrences)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(stepDef.Condition))
                return true;

            if (string.IsNullOrWhiteSpace(stepDef.DependsOnStepIds))
                return true;

            var allDepIds = stepDef.DependsOnStepIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            // Split by || → OR groups; any group true → overall true (|| has lower precedence than &&)
            var orGroups = stepDef.Condition.Split([" || "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var orGroup in orGroups)
            {
                // Split by && → AND clauses; all must be true
                var andClauses = orGroup.Split([" && "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (andClauses.All(clause => EvaluateClause(clause.Trim(), allDepIds, stepOccurrences)))
                    return true;
            }

            return false;
        }
        catch
        {
            return true; // On error, default to executing
        }
    }

    /// <summary>
    /// Evaluates a single clause (no and/or operators).
    /// Format: [stepId:](@status|$.field) operator value
    /// </summary>
    private static bool EvaluateClause(string clause, List<Guid> allDepIds, Dictionary<Guid, JobOccurrence> stepOccurrences)
    {
        try
        {
            // Optional stepId: prefix to target a specific parent
            List<Guid> targetIds;
            string expression;

            var colonIdx = clause.IndexOf(':');

            if (colonIdx > 0 && Guid.TryParse(clause[..colonIdx], out var specificId))
            {
                targetIds = [specificId];
                expression = clause[(colonIdx + 1)..].Trim();
            }
            else
            {
                targetIds = allDepIds;
                expression = clause;
            }

            // @status — checks WorkflowStepStatus of target parent(s); ALL must satisfy
            if (expression.StartsWith("@status"))
            {
                var opAndValue = expression[7..].Trim();
                var op = opAndValue.StartsWith("!=") ? "!=" : "==";
                var expectedValue = opAndValue[op.Length..].Trim().Trim('\'', '"');

                foreach (var depId in targetIds)
                {
                    if (!stepOccurrences.TryGetValue(depId, out var depOcc))
                        return false;

                    var statusStr = depOcc.StepStatus.ToString();

                    if (op == "==" && !string.Equals(statusStr, expectedValue, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (op == "!=" && string.Equals(statusStr, expectedValue, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            }

            // $.field — checks result JSON of target parent(s); ALL parents that have a result must satisfy
            if (expression.StartsWith("$."))
            {
                string matchedOp = null;
                string[] parts = null;

                foreach (var candidate in (ReadOnlySpan<string>)[" >= ", " <= ", " == ", " != ", " > ", " < "])
                {
                    parts = expression[2..].Split([candidate], 2, StringSplitOptions.TrimEntries);

                    if (parts.Length == 2)
                    {
                        matchedOp = candidate.Trim();
                        break;
                    }
                }

                if (matchedOp == null)
                    return true;

                var fieldPath = parts[0];
                var expectedValue = parts[1].Trim('\'', '"');

                foreach (var depId in targetIds)
                {
                    if (!stepOccurrences.TryGetValue(depId, out var depOcc) || string.IsNullOrWhiteSpace(depOcc.Result))
                        continue;

                    try
                    {
                        var json = JsonNode.Parse(depOcc.Result);

                        if (json == null)
                            continue;

                        if (!CompareValues(GetJsonValue(json, fieldPath)?.ToString(), expectedValue, matchedOp))
                            return false;
                    }
                    catch (JsonException) { }
                }

                return true;
            }

            return true; // Unknown format → default execute
        }
        catch
        {
            return true;
        }
    }

    private static bool CompareValues(string actual, string expected, string op) => op switch
    {
        "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        ">" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e) => a > e,
        "<" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e) => a < e,
        ">=" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e) => a >= e,
        "<=" when double.TryParse(actual, out var a) && double.TryParse(expected, out var e) => a <= e,
        _ => true
    };

    /// <summary>
    /// Updates all pre-created step occurrences to Queued/Running, bulk-inserts dispatch logs,
    /// then publishes to RabbitMQ. Failures are bulk-saved in a single pass.
    /// </summary>
    private async Task BatchDispatchStepsAsync(
        MilvaionDbContext dbContext,
        IJobOccurrenceEventPublisher eventPublisher,
        List<(WorkflowRun Run, JobOccurrence Occurrence, WorkflowStep StepDef, ScheduledJob Job, string JobData)> dispatches,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var logs = new List<JobOccurrenceLog>(dispatches.Count);

        foreach (var (run, occ, stepDef, job, _) in dispatches)
        {
            occ.Status = JobOccurrenceStatus.Queued;
            occ.StepStatus = WorkflowStepStatus.Running;
            occ.JobName = job.JobNameInWorker;
            occ.ZombieTimeoutMinutes = job.ZombieTimeoutMinutes;
            occ.JobVersion = job.Version;
            occ.ExecutionTimeoutSeconds = job.ExecutionTimeoutSeconds;

            logs.Add(new JobOccurrenceLog
            {
                Id = Guid.CreateVersion7(),
                OccurrenceId = occ.Id,
                Timestamp = now,
                Level = "Information",
                Message = $"Workflow step '{stepDef.StepName}' dispatched (Run: {run.Id})",
                Category = "WorkflowEngine",
                Data = new Dictionary<string, object>
                {
                    ["WorkflowRunId"] = run.Id.ToString(),
                    ["WorkflowStepId"] = stepDef.Id.ToString(),
                    ["StepName"] = stepDef.StepName,
                    ["RetryCount"] = occ.StepRetryCount,
                }
            });
        }

        // Atomically persist all occurrence status changes and logs before any RabbitMQ publish
        await dbContext.BulkUpdateAsync(dispatches.Select(d => d.Occurrence).ToList(), bc => bc.PropertiesToIncludeOnUpdate = _stepOccurrenceDispatchProps, cancellationToken: cancellationToken);
        await dbContext.BulkInsertAsync(logs, cancellationToken: cancellationToken);

        // Publish to RabbitMQ and collect results
        var failedOccurrences = new List<JobOccurrence>();
        var succeededOccurrences = new List<JobOccurrence>();

        for (var i = 0; i < dispatches.Count; i++)
        {
            var (run, occ, stepDef, job, jobData) = dispatches[i];

            var jobForDispatch = new ScheduledJob
            {
                Id = job.Id,
                JobNameInWorker = job.JobNameInWorker,
                JobData = jobData,
                WorkerId = job.WorkerId,
                RoutingPattern = job.RoutingPattern,
                ZombieTimeoutMinutes = job.ZombieTimeoutMinutes,
                ExecutionTimeoutSeconds = job.ExecutionTimeoutSeconds,
                Version = job.Version,
                DisplayName = job.DisplayName,
            };

            var published = await _rabbitMQPublisher.PublishJobAsync(jobForDispatch, occ.Id, cancellationToken);

            if (!published)
            {
                occ.StepStatus = WorkflowStepStatus.Failed;
                occ.Exception = "Failed to publish to RabbitMQ";
                occ.Status = JobOccurrenceStatus.Failed;
                failedOccurrences.Add(occ);
            }
            else
            {
                succeededOccurrences.Add(occ);
                _logger.Debug("Dispatched workflow step '{StepName}' for run {RunId}, occurrence {OccurrenceId}", stepDef.StepName, run.Id, occ.Id);
            }
        }

        // Bulk save all publish failures in one pass
        if (failedOccurrences.Count > 0)
            await dbContext.BulkUpdateAsync(failedOccurrences, bc => bc.PropertiesToIncludeOnUpdate = _stepOccurrenceFailProps, cancellationToken: cancellationToken);

        if (succeededOccurrences.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _redisStatsService.IncrementTotalOccurrencesAsync(succeededOccurrences.Count, cancellationToken);
                    await _redisStatsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Queued, succeededOccurrences.Count, cancellationToken);
                }
                catch
                {
                    // Non-critical
                }
            }, CancellationToken.None);

            if (eventPublisher != null)
                await eventPublisher.PublishOccurrenceCreatedAsync(succeededOccurrences, _logger, cancellationToken);
        }
    }

    /// <summary>
    /// Applies data mappings from parent step outputs to the current step's job data.
    /// </summary>
    private static string ApplyDataMappings(string jobData, string dataMappingsJson, Dictionary<Guid, JobOccurrence> stepOccurrences, Dictionary<Guid, WorkflowStep> stepDefinitions)
    {
        try
        {
            var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(dataMappingsJson);

            if (mappings == null || mappings.Count == 0)
                return jobData;

            var targetJson = !string.IsNullOrWhiteSpace(jobData) ? JsonNode.Parse(jobData)?.AsObject() : new JsonObject();

            targetJson ??= [];

            foreach (var (source, target) in mappings)
            {
                // Source format: "stepId:jsonPath" or just "jsonPath" (from first parent)
                var sourceParts = source.Split(':', 2);

                string sourceOutput = null;

                if (sourceParts.Length == 2 && Guid.TryParse(sourceParts[0], out var sourceStepId))
                {
                    if (stepOccurrences.TryGetValue(sourceStepId, out var sourceOcc))
                        sourceOutput = sourceOcc.Result;
                }
                else
                {
                    // Use first completed parent's output
                    foreach (var occ in stepOccurrences.Values.Where(o => o.StepStatus == WorkflowStepStatus.Completed && !string.IsNullOrWhiteSpace(o.Result)))
                    {
                        sourceOutput = occ.Result;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(sourceOutput))
                    continue;

                try
                {
                    var sourceJson = JsonNode.Parse(sourceOutput);
                    var jsonPath = sourceParts.Length == 2 ? sourceParts[1] : sourceParts[0];

                    // Traverse dot-separated path (e.g., "ComplexProp.Title")
                    var value = GetJsonValue(sourceJson, jsonPath)?.DeepClone();

                    if (value != null)
                        SetJsonValue(targetJson, target, value);
                }
                catch
                {
                    // Skip invalid mappings
                }
            }

            return targetJson.ToJsonString();
        }
        catch
        {
            return jobData;
        }
    }

    private static JsonNode GetJsonValue(JsonNode json, string path)
    {
        var segments = path.TrimStart('$', '.').Split('.');
        var current = json;

        foreach (var segment in segments)
        {
            if (current == null)
                return null;

            current = current[segment];
        }

        return current;
    }

    private static void SetJsonValue(JsonObject target, string path, JsonNode value)
    {
        var segments = path.TrimStart('$', '.').Split('.');
        var current = target;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is not JsonObject nested)
            {
                nested = [];
                current[segments[i]] = nested;
            }

            current = nested;
        }

        current[segments[^1]] = value;
    }

    private void CancelRunInMemory(WorkflowRun run, DateTime now, string reason, List<WorkflowRun> runsToUpdate, List<JobOccurrence> occurrencesToUpdate, List<JobOccurrence> runningOccurrencesToCancel)
    {
        run.Status = WorkflowStatus.Cancelled;
        run.EndTime = now;
        run.Error = reason;

        if (run.StartTime.HasValue)
            run.DurationMs = (long)(now - run.StartTime.Value).TotalMilliseconds;

        var runningToCancel = run.StepOccurrences
            .Where(o => o.StepStatus == WorkflowStepStatus.Running)
            .ToList();

        var pendingToSkip = run.StepOccurrences
            .Where(o => o.StepStatus is WorkflowStepStatus.Pending or WorkflowStepStatus.Delayed)
            .ToList();

        runningOccurrencesToCancel.AddRange(runningToCancel);

        foreach (var occ in runningToCancel)
        {
            occ.StepStatus = WorkflowStepStatus.Cancelled;
            occ.Status = JobOccurrenceStatus.Cancelled;
            occ.EndTime = now;
        }

        foreach (var occ in pendingToSkip)
        {
            occ.StepStatus = WorkflowStepStatus.Skipped;
            occ.Status = JobOccurrenceStatus.Skipped;
            occ.EndTime = now;
            occ.Exception = $"Step skipped due to workflow cancellation: {reason}";
        }

        runsToUpdate.Add(run);
        occurrencesToUpdate.AddRange(runningToCancel);
        occurrencesToUpdate.AddRange(pendingToSkip);

        _logger.Warning("Workflow run {RunId} cancelled: {Reason}", run.Id, reason);
    }

    private void FailRunInMemory(WorkflowRun run, DateTime now, string reason, List<WorkflowRun> runsToUpdate, List<JobOccurrence> occurrencesToUpdate, List<JobOccurrence> runningOccurrencesToCancel)
    {
        run.Status = WorkflowStatus.Failed;
        run.EndTime = now;
        run.Error = reason;

        if (run.StartTime.HasValue)
            run.DurationMs = (long)(now - run.StartTime.Value).TotalMilliseconds;

        var runningToCancel = run.StepOccurrences
            .Where(o => o.StepStatus == WorkflowStepStatus.Running)
            .ToList();

        var pendingToSkip = run.StepOccurrences
            .Where(o => o.StepStatus is WorkflowStepStatus.Pending or WorkflowStepStatus.Delayed)
            .ToList();

        runningOccurrencesToCancel.AddRange(runningToCancel);

        foreach (var occ in runningToCancel)
        {
            occ.StepStatus = WorkflowStepStatus.Cancelled;
            occ.Status = JobOccurrenceStatus.Cancelled;
            occ.EndTime = now;
        }

        foreach (var occ in pendingToSkip)
        {
            occ.StepStatus = WorkflowStepStatus.Skipped;
            occ.Status = JobOccurrenceStatus.Skipped;
            occ.EndTime = now;
            occ.Exception = $"Step skipped due to workflow failure: {reason}";
        }

        runsToUpdate.Add(run);
        occurrencesToUpdate.AddRange(runningToCancel);
        occurrencesToUpdate.AddRange(pendingToSkip);

        _logger.Warning("Workflow run {RunId} failed: {Reason}", run.Id, reason);
    }
}
