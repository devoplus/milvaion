using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvaion.Application.Features.Workflows.CancelWorkflow;
using Milvaion.Application.Features.Workflows.CreateWorkflow;
using Milvaion.Application.Features.Workflows.TriggerWorkflow;
using Milvaion.Application.Features.Workflows.UpdateWorkflow;
using Milvaion.IntegrationTests.BackgroundServices;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using System.Net;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.ControllersTests;

/// <summary>
/// Integration tests for WorkflowsController.
/// Tests workflow CRUD operations, triggering, and execution tracking.
/// According to copilot instructions, integration tests should obtain services like IMilvaLogger directly from the ServiceProvider instead of using stubs or mocks.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class WorkflowsControllerTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : BackgroundServiceTestBase(factory, output)
{
    [Fact]
    public async Task GetWorkflows_ShouldReturnPaginatedList()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        // Create test workflow
        var workflow = await SeedWorkflowAsync("Test Workflow");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/workflows")
        {
            Content = JsonContent.Create(new
            {
                pageNumber = 1,
                rowCount = 10
            })
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(workflow.Id.ToString());
    }

    [Fact]
    public async Task GetWorkflowById_ShouldReturnWorkflowDetails()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Test Workflow Detail");

        // Act
        var response = await client.GetAsync($"/api/v1/workflows/workflow?workflowId={workflow.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Test Workflow Detail");
        content.Should().Contain(workflow.Id.ToString());
    }

    [Fact]
    public async Task CreateWorkflow_WithValidData_ShouldCreateSuccessfully()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var job1 = await SeedScheduledJobAsync($"Job1_{Guid.CreateVersion7():N}");
        var job2 = await SeedScheduledJobAsync($"Job2_{Guid.CreateVersion7():N}");

        var command = new CreateWorkflowCommand
        {
            Name = "Integration Test Workflow",
            Description = "Workflow created in integration test",
            IsActive = true,
            FailureStrategy = WorkflowFailureStrategy.StopOnFirstFailure,
            MaxStepRetries = 3,
            TimeoutSeconds = 3600,
            Tags = "test,integration",
            Steps =
            [
                new CreateWorkflowStepDto
                {
                    TempId = "step1",
                    StepName = "First Step",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job1.Id,
                    Order = 1
                },
                new CreateWorkflowStepDto
                {
                    TempId = "step2",
                    StepName = "Second Step",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job2.Id,
                    Order = 2
                }
            ],
            Edges =
            [
                new CreateWorkflowEdgeDto
                {
                    TempId = "edge1",
                    SourceTempId = "step1",
                    TargetTempId = "step2",
                    Order = 1
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeEmpty();

        // Verify workflow was created in database
        var dbContext = GetDbContext();
        var createdWorkflow = await dbContext.Workflows.FindAsync(result.Data);
        createdWorkflow.Should().NotBeNull();
        createdWorkflow!.Name.Should().Be("Integration Test Workflow");
    }

    [Fact]
    public async Task CreateWorkflow_WithInvalidCronExpression_ShouldReturnBadRequest()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var job = await SeedScheduledJobAsync($"Job_{Guid.CreateVersion7():N}");

        var command = new CreateWorkflowCommand
        {
            Name = "Invalid Cron Workflow",
            CronExpression = "invalid cron",
            Steps =
            [
                new CreateWorkflowStepDto
                {
                    TempId = "step1",
                    StepName = "Step 1",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job.Id,
                    Order = 1
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow", command);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Message.Contains("cron", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateWorkflow_WithValidData_ShouldUpdateSuccessfully()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Original Workflow");
        var job = await SeedScheduledJobAsync($"UpdatedJob_{Guid.CreateVersion7():N}");

        var command = new UpdateWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Name = "Updated Workflow",
            Description = "Updated description",
            IsActive = false,
            FailureStrategy = WorkflowFailureStrategy.ContinueOnFailure,
            MaxStepRetries = 5,
            TimeoutSeconds = 7200,
            Tags = "updated,test",
            Steps =
            [
                new CreateWorkflowStepDto
                {
                    TempId = "step1",
                    StepName = "Updated Step",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job.Id,
                    Order = 1
                }
            ],
            Edges = []
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/workflows/workflow", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Verify workflow was updated
        var dbContext = GetDbContext();
        var updatedWorkflow = await dbContext.Workflows.FindAsync(workflow.Id);
        updatedWorkflow.Should().NotBeNull();
        updatedWorkflow!.Name.Should().Be("Updated Workflow");
    }

    [Fact]
    public async Task DeleteWorkflow_ShouldDeleteSuccessfully()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Workflow To Delete");

        // Act
        var response = await client.DeleteAsync($"/api/v1/workflows/workflow?workflowId={workflow.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify workflow is deleted
        var dbContext = GetDbContext();
        var deletedWorkflow = await dbContext.Workflows.FindAsync(workflow.Id);
        deletedWorkflow.Should().BeNull();
    }

    [Fact]
    public async Task TriggerWorkflow_ShouldCreateWorkflowRun()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Workflow To Trigger", isActive: true);

        var command = new TriggerWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Reason = "Integration test trigger"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow/trigger", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Verify run was created
        var dbContext = GetDbContext();
        var createdRun = await dbContext.WorkflowRuns.FirstOrDefaultAsync(r => r.WorkflowId == workflow.Id);
        createdRun.Should().NotBeNull();
        createdRun!.TriggerReason.Should().Be("Integration test trigger");
    }

    [Fact]
    public async Task TriggerWorkflow_InactiveWorkflow_ShouldReturnBadRequest()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Inactive Workflow", isActive: false);

        var command = new TriggerWorkflowCommand
        {
            WorkflowId = workflow.Id,
            Reason = "Should fail"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow/trigger", command);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeFalse();
        result.Messages.Should().Contain(m => m.Message.Contains("not active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetWorkflowRuns_ShouldReturnPaginatedRuns()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Workflow With Runs");
        var run = await SeedWorkflowRunAsync(workflow.Id, WorkflowStatus.Running);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/workflows/runs")
        {
            Content = JsonContent.Create(new
            {
                pageNumber = 1,
                rowCount = 10
            })
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(run.Id.ToString());
    }

    [Fact]
    public async Task GetWorkflowRunDetail_ShouldReturnRunWithSteps()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Workflow For Run Detail");
        var run = await SeedWorkflowRunAsync(workflow.Id, WorkflowStatus.Completed);

        // Act
        var response = await client.GetAsync($"/api/v1/workflows/runs/run?runId={run.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(run.Id.ToString());
    }

    [Fact]
    public async Task CancelWorkflow_ShouldCancelRunningRun()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var workflow = await SeedWorkflowAsync("Workflow To Cancel");
        var run = await SeedWorkflowRunAsync(workflow.Id, WorkflowStatus.Running);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow/cancel", new CancelWorkflowCommand { WorkflowRunId = run.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify run is cancelled
        var dbContext = GetDbContext();
        var cancelledRun = await dbContext.WorkflowRuns.FindAsync(run.Id);
        cancelledRun.Should().NotBeNull();
        cancelledRun!.Status.Should().Be(WorkflowStatus.Cancelled);
    }

    [Fact]
    public async Task CreateWorkflow_WithConditionalBranching_ShouldCreateSuccessfully()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var job1 = await SeedScheduledJobAsync($"TruePathJob_{Guid.CreateVersion7():N}");
        var job2 = await SeedScheduledJobAsync($"FalsePathJob_{Guid.CreateVersion7():N}");

        var command = new CreateWorkflowCommand
        {
            Name = "Conditional Workflow",
            Description = "Workflow with conditional branching",
            Steps =
            [
                new CreateWorkflowStepDto
                {
                    TempId = "condition",
                    StepName = "Check Condition",
                    NodeType = WorkflowNodeType.Condition,
                    NodeConfigJson = @"{""expression"": ""result.status == 'success'""}",
                    Order = 1
                },
                new CreateWorkflowStepDto
                {
                    TempId = "trueTask",
                    StepName = "True Path",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job1.Id,
                    Order = 2
                },
                new CreateWorkflowStepDto
                {
                    TempId = "falseTask",
                    StepName = "False Path",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job2.Id,
                    Order = 3
                }
            ],
            Edges =
            [
                new CreateWorkflowEdgeDto
                {
                    TempId = "edgeTrue",
                    SourceTempId = "condition",
                    TargetTempId = "trueTask",
                    SourcePort = "true",
                    Order = 1
                },
                new CreateWorkflowEdgeDto
                {
                    TempId = "edgeFalse",
                    SourceTempId = "condition",
                    TargetTempId = "falseTask",
                    SourcePort = "false",
                    Order = 2
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Verify workflow was created
        var dbContext = GetDbContext();
        var createdWorkflow = await dbContext.Workflows.FindAsync(result.Data);
        createdWorkflow.Should().NotBeNull();
        createdWorkflow!.Name.Should().Be("Conditional Workflow");
    }

    [Fact]
    public async Task CreateWorkflow_WithDataMapping_ShouldCreateSuccessfully()
    {
        // Arrange
        await InitializeAsync();
        var client = await GetClient();

        var job1 = await SeedScheduledJobAsync($"ExtractJob_{Guid.CreateVersion7():N}");
        var job2 = await SeedScheduledJobAsync($"TransformJob_{Guid.CreateVersion7():N}");

        var command = new CreateWorkflowCommand
        {
            Name = "Data Mapping Workflow",
            Description = "Workflow with data mappings between steps",
            Steps =
            [
                new CreateWorkflowStepDto
                {
                    TempId = "extract",
                    StepName = "Extract Data",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job1.Id,
                    Order = 1
                },
                new CreateWorkflowStepDto
                {
                    TempId = "transform",
                    StepName = "Transform Data",
                    NodeType = WorkflowNodeType.Task,
                    JobId = job2.Id,
                    Order = 2,
                    DataMappings = @"{""extract:result.userId"": ""inputUserId"", ""extract:result.data"": ""inputData""}"
                }
            ],
            Edges =
            [
                new CreateWorkflowEdgeDto
                {
                    TempId = "edge1",
                    SourceTempId = "extract",
                    TargetTempId = "transform",
                    Order = 1
                }
            ]
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workflows/workflow", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();

        // Verify workflow was created
        var dbContext = GetDbContext();
        var createdWorkflow = await dbContext.Workflows.FindAsync(result.Data);
        createdWorkflow.Should().NotBeNull();
        createdWorkflow!.Name.Should().Be("Data Mapping Workflow");
    }

    private async Task<HttpClient> GetClient()
    {
        var client = _factory.CreateClient();

        // Seed root user if not exists
        await SeedRootUserAndSuperAdminRoleAsync();

        // Login to get auth token
        await client.LoginAsync();

        return client;
    }

    private async Task<Workflow> SeedWorkflowAsync(string name, bool isActive = true, WorkflowFailureStrategy strategy = WorkflowFailureStrategy.StopOnFirstFailure)
    {
        var dbContext = GetDbContext();

        var job = await SeedScheduledJobAsync($"WorkflowJob_{Guid.CreateVersion7():N}");

        var workflow = new Workflow
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Description = $"Test workflow: {name}",
            IsActive = isActive,
            FailureStrategy = strategy,
            MaxStepRetries = 3,
            TimeoutSeconds = 3600,
            Version = 1,
            Tags = "test",
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

        dbContext.Workflows.Add(workflow);
        await dbContext.SaveChangesAsync();

        return workflow;
    }

    private async Task<WorkflowRun> SeedWorkflowRunAsync(Guid workflowId, WorkflowStatus status)
    {
        var dbContext = GetDbContext();

        var run = new WorkflowRun
        {
            Id = Guid.CreateVersion7(),
            WorkflowId = workflowId,
            Status = status,
            TriggerReason = "Test run",
            StartTime = status != WorkflowStatus.Pending ? DateTime.UtcNow.AddMinutes(-10) : null,
            EndTime = status == WorkflowStatus.Completed || status == WorkflowStatus.Failed || status == WorkflowStatus.Cancelled ? DateTime.UtcNow : null
        };

        if (run.StartTime.HasValue && run.EndTime.HasValue)
            run.DurationMs = (int)(run.EndTime.Value - run.StartTime.Value).TotalMilliseconds;

        dbContext.WorkflowRuns.Add(run);
        await dbContext.SaveChangesAsync();

        return run;
    }
}
