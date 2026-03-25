using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.BackgroundServices;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.BackgroundServices;

/// <summary>
/// Integration tests for WorkflowEngineService.
/// Tests workflow execution, step scheduling, dependency resolution, and failure handling.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class WorkflowEngineServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : BackgroundServiceTestBase(factory, output)
{
    [Fact]
    public async Task WorkflowEngine_ShouldStartPendingWorkflowRun()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var workflow = await SeedWorkflowWithSingleStepAsync("Test Workflow");

        // Trigger workflow using the command handler to properly create step occurrences
        var mediator = _serviceProvider.GetRequiredService<MediatR.IMediator>();
        var triggerResult = await mediator.Send(new Milvaion.Application.Features.Workflows.TriggerWorkflow.TriggerWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Reason = "Test run"
        });

        var runId = triggerResult.Data;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();

        var engineTask = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _output.WriteLine("Workflow engine cancelled");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Workflow engine error: {ex}");
                throw;
            }
        }, cts.Token);

        // Wait for run to start (it may complete quickly, so accept Running or Completed)
        var started = await WaitForConditionAsync(
            async () =>
            {
                var dbContext = GetDbContext();
                var updatedRun = await dbContext.WorkflowRuns.FindAsync(runId);
                return updatedRun?.Status == WorkflowStatus.Running;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await cts.CancelAsync();
        await engine.StopAsync(CancellationToken.None);

        // Check if engine task had exception
        if (engineTask.IsFaulted)
        {
            throw engineTask.Exception!.GetBaseException();
        }

        // Assert
        started.Should().BeTrue("workflow run should transition from Pending to Running/Completed");

        var dbContextAssert = GetDbContext();
        var finalRun = await dbContextAssert.WorkflowRuns.FindAsync(runId);
        finalRun.Should().NotBeNull();
        finalRun!.Status.Should().BeOneOf(WorkflowStatus.Running, WorkflowStatus.Completed);
        finalRun.StartTime.Should().NotBeNull("workflow should have a start time");
    }

    [Fact]
    public async Task WorkflowEngine_ShouldDispatchReadySteps()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"WorkflowJob_{Guid.CreateVersion7():N}");
        var workflow = await SeedWorkflowWithStepsAsync("Multi-Step Workflow", [job.Id]);

        // Trigger workflow using the command handler to properly create step occurrences
        var mediator = _serviceProvider.GetRequiredService<MediatR.IMediator>();
        var triggerResult = await mediator.Send(new Milvaion.Application.Features.Workflows.TriggerWorkflow.TriggerWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Reason = "Test run"
        });

        var runId = triggerResult.Data;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for step occurrence to be dispatched (Status changed to Queued)
        var dispatched = await WaitForConditionAsync(
            async () =>
            {
                var dbContext = GetDbContext();
                var occurrences = await dbContext.JobOccurrences
                    .Where(o => o.WorkflowRunId == runId)
                    .ToListAsync(cts.Token);
                return occurrences.Count > 0 && occurrences.Any(o => o.Status == JobOccurrenceStatus.Queued);
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await engine.StopAsync(cts.Token);

        // Assert
        dispatched.Should().BeTrue("workflow engine should dispatch ready steps");

        var dbContextAssert = GetDbContext();
        var stepOccurrences = await dbContextAssert.JobOccurrences
            .Where(o => o.WorkflowRunId == runId)
            .ToListAsync(cts.Token);

        stepOccurrences.Should().NotBeEmpty();
        stepOccurrences.Should().Contain(o => o.Status == JobOccurrenceStatus.Queued);
    }

    [Fact]
    public async Task WorkflowEngine_ShouldHandleCronScheduledWorkflows()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"CronJob_{Guid.CreateVersion7():N}");

        // Create workflow with cron that should have already executed
        var workflow = await SeedWorkflowAsync("Cron Workflow", isActive: true, cronExpression: "0 0 * * * *"); // Every hour
        workflow.LastScheduledRunAt = DateTime.UtcNow.AddHours(-2); // Last run was 2 hours ago

        var dbContext = GetDbContext();
        dbContext.Workflows.Update(workflow);
        await dbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for workflow run to be created by cron trigger
        var triggered = await WaitForConditionAsync(
            async () =>
            {
                var db = GetDbContext();
                var runs = await db.WorkflowRuns
                    .Where(r => r.WorkflowId == workflow.Id)
                    .ToListAsync(cts.Token);
                return runs.Count > 0;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await engine.StopAsync(cts.Token);

        // Assert
        triggered.Should().BeTrue("workflow should be triggered by cron schedule");

        var dbContextAssert = GetDbContext();
        var cronRuns = await dbContextAssert.WorkflowRuns
            .Where(r => r.WorkflowId == workflow.Id)
            .ToListAsync(cts.Token);

        cronRuns.Should().NotBeEmpty();
        cronRuns.Should().Contain(r => r.TriggerReason == "Cron schedule");
    }

    [Fact]
    public async Task WorkflowEngine_ShouldRespectStepDependencies()
    {
        // Arrange
        await InitializeAsync();

        var job1 = await SeedScheduledJobAsync($"Step1Job_{Guid.CreateVersion7():N}");
        var job2 = await SeedScheduledJobAsync($"Step2Job_{Guid.CreateVersion7():N}");

        var step1Id = Guid.CreateVersion7();
        var step2Id = Guid.CreateVersion7();

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = "Dependency Test Workflow",
            IsActive = true,
            FailureStrategy = WorkflowFailureStrategy.StopOnFirstFailure,
            Definition = new WorkflowDefinition
            {
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = step1Id,
                        StepName = "Step 1",
                        NodeType = WorkflowNodeType.Task,
                        JobId = job1.Id,
                        Order = 1
                    },
                    new WorkflowStepDefinition
                    {
                        Id = step2Id,
                        StepName = "Step 2 (depends on Step 1)",
                        NodeType = WorkflowNodeType.Task,
                        JobId = job2.Id,
                        Order = 2
                    }
                ],
                Edges =
                [
                    new WorkflowEdgeDefinition
                    {
                        SourceStepId = step1Id,
                        TargetStepId = step2Id,
                        Order = 1
                    }
                ]
            }
        };

        var dbContext = GetDbContext();
        dbContext.Workflows.Add(workflow);
        await dbContext.SaveChangesAsync();

        // Trigger workflow using the command handler to properly create step occurrences
        var mediator = _serviceProvider.GetRequiredService<MediatR.IMediator>();
        var triggerResult = await mediator.Send(new Milvaion.Application.Features.Workflows.TriggerWorkflow.TriggerWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Reason = "Test run"
        });

        var runId = triggerResult.Data;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for first step to be dispatched
        var step1Dispatched = await WaitForConditionAsync(
            async () =>
            {
                var db = GetDbContext();
                var occurrences = await db.JobOccurrences
                    .Where(o => o.WorkflowRunId == runId && o.WorkflowStepId == step1Id)
                    .ToListAsync(cts.Token);
                return occurrences.Count > 0 && occurrences.Any(o => o.Status == JobOccurrenceStatus.Queued);
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await engine.StopAsync(cts.Token);

        // Assert - Step 1 should be dispatched, but Step 2 should not (because Step 1 is not completed)
        step1Dispatched.Should().BeTrue("first step should be dispatched");

        var dbContextAssert = GetDbContext();
        var step1Occurrences = await dbContextAssert.JobOccurrences
            .Where(o => o.WorkflowRunId == runId && o.WorkflowStepId == step1Id)
            .ToListAsync(cts.Token);

        var step2Occurrences = await dbContextAssert.JobOccurrences
            .Where(o => o.WorkflowRunId == runId && o.WorkflowStepId == step2Id)
            .ToListAsync(cts.Token);

        step1Occurrences.Should().NotBeEmpty("Step 1 should be dispatched");
        step1Occurrences.Should().Contain(o => o.Status == JobOccurrenceStatus.Queued, "Step 1 should be queued");
        step2Occurrences.Should().OnlyContain(o => o.StepStatus == WorkflowStepStatus.Pending, "Step 2 should remain pending until Step 1 completes");
    }

    [Fact]
    public async Task WorkflowEngine_ShouldHandleWorkflowTimeout()
    {
        // Arrange
        await InitializeAsync();

        var workflow = await SeedWorkflowWithSingleStepAsync("Timeout Test Workflow", timeoutSeconds: 5);
        var run = await SeedWorkflowRunAsync(workflow.Id, WorkflowStatus.Running, startTime: DateTime.UtcNow.AddSeconds(-10)); // Started 10 seconds ago

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for run to be cancelled due to timeout
        var timedOut = await WaitForConditionAsync(
            async () =>
            {
                var dbContext = GetDbContext();
                var updatedRun = await dbContext.WorkflowRuns.FindAsync(run.Id);
                return updatedRun?.Status == WorkflowStatus.Cancelled;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await engine.StopAsync(cts.Token);

        // Assert
        timedOut.Should().BeTrue("workflow should be cancelled when timeout is exceeded");

        var dbContextAssert = GetDbContext();
        var finalRun = await dbContextAssert.WorkflowRuns.FindAsync(run.Id);
        finalRun.Should().NotBeNull();
        finalRun!.Status.Should().Be(WorkflowStatus.Cancelled);
        finalRun.Error.Should().Contain("timed out");
    }

    [Fact]
    public async Task WorkflowEngine_WithStopOnFirstFailure_ShouldStopWorkflow()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"FailingJob_{Guid.CreateVersion7():N}");
        var workflow = await SeedWorkflowWithStepsAsync("Failure Test Workflow", [job.Id], failureStrategy: WorkflowFailureStrategy.StopOnFirstFailure);
        var run = await SeedWorkflowRunAsync(workflow.Id, WorkflowStatus.Running);

        // Create a failed step occurrence
        var stepId = workflow.Definition.Steps.First().Id;
        var failedOccurrence = new JobOccurrence
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            WorkflowRunId = run.Id,
            WorkflowStepId = stepId,
            StepStatus = WorkflowStepStatus.Failed,
            StepRetryCount = 0,
            Status = JobOccurrenceStatus.Failed,
            Exception = "Test failure",
            JobName = job.JobNameInWorker
        };

        var dbContext = GetDbContext();
        dbContext.JobOccurrences.Add(failedOccurrence);
        await dbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Start workflow engine
        var engine = CreateWorkflowEngineService();
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for workflow to fail
        var failed = await WaitForConditionAsync(
            async () =>
            {
                var db = GetDbContext();
                var updatedRun = await db.WorkflowRuns.FindAsync(run.Id);
                return updatedRun?.Status == WorkflowStatus.Failed;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await engine.StopAsync(cts.Token);

        // Assert
        failed.Should().BeTrue("workflow should fail when a step fails with StopOnFirstFailure strategy");

        var dbContextAssert = GetDbContext();
        var finalRun = await dbContextAssert.WorkflowRuns.FindAsync(run.Id);
        finalRun.Should().NotBeNull();
        finalRun!.Status.Should().Be(WorkflowStatus.Failed);
    }

    private WorkflowEngineService CreateWorkflowEngineService() => new(
        _serviceProvider,
        Options.Create(new WorkflowEngineOptions
        {
            Enabled = true,
            PollingIntervalSeconds = 1,
        }),
        _serviceProvider.GetRequiredService<ILoggerFactory>(),
        null
        );

    private async Task<Workflow> SeedWorkflowWithSingleStepAsync(string name, bool isActive = true, int timeoutSeconds = 3600)
    {
        var job = await SeedScheduledJobAsync($"WorkflowJob_{Guid.CreateVersion7():N}");

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            IsActive = isActive,
            TimeoutSeconds = timeoutSeconds,
            FailureStrategy = WorkflowFailureStrategy.StopOnFirstFailure,
            MaxStepRetries = 0,
            Definition = new WorkflowDefinition
            {
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = Guid.CreateVersion7(),
                        StepName = "Test Step",
                        NodeType = WorkflowNodeType.Task,
                        JobId = job.Id,
                        Order = 1
                    }
                ],
                Edges = []
            }
        };

        var dbContext = GetDbContext();
        dbContext.Workflows.Add(workflow);
        await dbContext.SaveChangesAsync();

        return workflow;
    }

    private async Task<Workflow> SeedWorkflowWithStepsAsync(string name, List<Guid> jobIds, WorkflowFailureStrategy failureStrategy = WorkflowFailureStrategy.StopOnFirstFailure, int maxStepRetries = 0)
    {
        var steps = jobIds.Select((jobId, index) => new WorkflowStepDefinition
        {
            Id = Guid.CreateVersion7(),
            StepName = $"Step {index + 1}",
            NodeType = WorkflowNodeType.Task,
            JobId = jobId,
            Order = index + 1
        }).ToList();

        var edges = new List<WorkflowEdgeDefinition>();
        for (int i = 0; i < steps.Count - 1; i++)
        {
            edges.Add(new WorkflowEdgeDefinition
            {
                SourceStepId = steps[i].Id,
                TargetStepId = steps[i + 1].Id,
                Order = i + 1
            });
        }

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            IsActive = true,
            FailureStrategy = failureStrategy,
            MaxStepRetries = maxStepRetries,
            Definition = new WorkflowDefinition
            {
                Steps = steps,
                Edges = edges
            }
        };

        var dbContext = GetDbContext();
        dbContext.Workflows.Add(workflow);
        await dbContext.SaveChangesAsync();

        return workflow;
    }

    private async Task<Workflow> SeedWorkflowAsync(string name, bool isActive = true, string cronExpression = null)
    {
        var job = await SeedScheduledJobAsync($"CronJob_{Guid.CreateVersion7():N}");

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            IsActive = isActive,
            CronExpression = cronExpression,
            FailureStrategy = WorkflowFailureStrategy.StopOnFirstFailure,
            Definition = new WorkflowDefinition
            {
                Steps =
                [
                    new WorkflowStepDefinition
                    {
                        Id = Guid.CreateVersion7(),
                        StepName = "Test Step",
                        NodeType = WorkflowNodeType.Task,
                        JobId = job.Id,
                        Order = 1
                    }
                ],
                Edges = []
            }
        };

        var dbContext = GetDbContext();
        dbContext.Workflows.Add(workflow);
        await dbContext.SaveChangesAsync();

        return workflow;
    }

    private async Task<WorkflowRun> SeedWorkflowRunAsync(Guid workflowId, WorkflowStatus status, DateTime? startTime = null)
    {
        var run = new WorkflowRun
        {
            Id = Guid.CreateVersion7(),
            WorkflowId = workflowId,
            Status = status,
            TriggerReason = "Test run",
            StartTime = startTime ?? (status != WorkflowStatus.Pending ? DateTime.UtcNow : null)
        };

        var dbContext = GetDbContext();
        dbContext.WorkflowRuns.Add(run);
        await dbContext.SaveChangesAsync();

        return run;
    }
}
