using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;

namespace Milvaion.UnitTests.SdkTests;

[Trait("SDK Unit Tests", "Workflow entity unit tests.")]
public class WorkflowEntityTests
{
    [Fact]
    public void Workflow_ShouldSetAndGetProperties()
    {
        // Arrange
        var workflow = new Workflow();
        var id = Guid.CreateVersion7();
        var name = "Test Workflow";
        var description = "Test Description";
        var tags = "tag1,tag2";
        var isActive = true;
        var failureStrategy = WorkflowFailureStrategy.ContinueOnFailure;
        var maxStepRetries = 3;
        var timeoutSeconds = 3600;
        var version = 2;
        var cronExpression = "0 0 * * * *";
        var lastScheduledRunAt = DateTime.UtcNow;
        var definition = new WorkflowDefinition();
        var versions = new List<WorkflowSnapshot>();
        var runs = new List<WorkflowRun>();

        // Act
        workflow.Id = id;
        workflow.Name = name;
        workflow.Description = description;
        workflow.Tags = tags;
        workflow.IsActive = isActive;
        workflow.FailureStrategy = failureStrategy;
        workflow.MaxStepRetries = maxStepRetries;
        workflow.TimeoutSeconds = timeoutSeconds;
        workflow.Version = version;
        workflow.CronExpression = cronExpression;
        workflow.LastScheduledRunAt = lastScheduledRunAt;
        workflow.Definition = definition;
        workflow.Versions = versions;
        workflow.Runs = runs;

        // Assert
        workflow.Id.Should().Be(id);
        workflow.Name.Should().Be(name);
        workflow.Description.Should().Be(description);
        workflow.Tags.Should().Be(tags);
        workflow.IsActive.Should().Be(isActive);
        workflow.FailureStrategy.Should().Be(failureStrategy);
        workflow.MaxStepRetries.Should().Be(maxStepRetries);
        workflow.TimeoutSeconds.Should().Be(timeoutSeconds);
        workflow.Version.Should().Be(version);
        workflow.CronExpression.Should().Be(cronExpression);
        workflow.LastScheduledRunAt.Should().Be(lastScheduledRunAt);
        workflow.Definition.Should().Be(definition);
        workflow.Versions.Should().BeEquivalentTo(versions);
        workflow.Runs.Should().BeEquivalentTo(runs);
    }

    [Fact]
    public void Workflow_ShouldHaveDefaultValues()
    {
        // Act
        var workflow = new Workflow();

        // Assert
        workflow.IsActive.Should().BeTrue();
        workflow.FailureStrategy.Should().Be(WorkflowFailureStrategy.StopOnFirstFailure);
        workflow.MaxStepRetries.Should().Be(0);
        workflow.Version.Should().Be(1);
        workflow.Definition.Should().NotBeNull();
        workflow.Versions.Should().NotBeNull();
        workflow.Versions.Should().BeEmpty();
    }
}

[Trait("SDK Unit Tests", "WorkflowRun entity unit tests.")]
public class WorkflowRunEntityTests
{
    [Fact]
    public void WorkflowRun_ShouldSetAndGetProperties()
    {
        // Arrange
        var workflowRun = new WorkflowRun();
        var id = Guid.CreateVersion7();
        var workflowId = Guid.CreateVersion7();
        var workflowVersion = 2;
        var correlationId = Guid.CreateVersion7();
        var status = WorkflowStatus.Running;
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddMinutes(5);
        var durationMs = 300000L;
        var triggerReason = "Manual trigger";
        var error = "Some error";
        var createdAt = DateTime.UtcNow;
        var workflow = new Workflow();
        var stepOccurrences = new List<JobOccurrence>();

        // Act
        workflowRun.Id = id;
        workflowRun.WorkflowId = workflowId;
        workflowRun.WorkflowVersion = workflowVersion;
        workflowRun.CorrelationId = correlationId;
        workflowRun.Status = status;
        workflowRun.StartTime = startTime;
        workflowRun.EndTime = endTime;
        workflowRun.DurationMs = durationMs;
        workflowRun.TriggerReason = triggerReason;
        workflowRun.Error = error;
        workflowRun.CreatedAt = createdAt;
        workflowRun.Workflow = workflow;
        workflowRun.StepOccurrences = stepOccurrences;

        // Assert
        workflowRun.Id.Should().Be(id);
        workflowRun.WorkflowId.Should().Be(workflowId);
        workflowRun.WorkflowVersion.Should().Be(workflowVersion);
        workflowRun.CorrelationId.Should().Be(correlationId);
        workflowRun.Status.Should().Be(status);
        workflowRun.StartTime.Should().Be(startTime);
        workflowRun.EndTime.Should().Be(endTime);
        workflowRun.DurationMs.Should().Be(durationMs);
        workflowRun.TriggerReason.Should().Be(triggerReason);
        workflowRun.Error.Should().Be(error);
        workflowRun.CreatedAt.Should().Be(createdAt);
        workflowRun.Workflow.Should().Be(workflow);
        workflowRun.StepOccurrences.Should().BeEquivalentTo(stepOccurrences);
    }

    [Fact]
    public void WorkflowRun_ShouldHaveDefaultValues()
    {
        // Act
        var workflowRun = new WorkflowRun();

        // Assert
        workflowRun.Status.Should().Be(WorkflowStatus.Pending);
        workflowRun.StepOccurrences.Should().NotBeNull();
        workflowRun.StepOccurrences.Should().BeEmpty();
        workflowRun.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}

[Trait("SDK Unit Tests", "WorkflowDefinition JSON model unit tests.")]
public class WorkflowDefinitionTests
{
    [Fact]
    public void WorkflowDefinition_ShouldSetAndGetProperties()
    {
        // Arrange
        var definition = new WorkflowDefinition();
        var steps = new List<WorkflowStepDefinition>
        {
            new() { Id = Guid.CreateVersion7(), StepName = "Step 1" }
        };
        var edges = new List<WorkflowEdgeDefinition>
        {
            new() { SourceStepId = Guid.CreateVersion7(), TargetStepId = Guid.CreateVersion7() }
        };

        // Act
        definition.Steps = steps;
        definition.Edges = edges;

        // Assert
        definition.Steps.Should().BeEquivalentTo(steps);
        definition.Edges.Should().BeEquivalentTo(edges);
    }

    [Fact]
    public void WorkflowDefinition_ShouldHaveDefaultValues()
    {
        // Act
        var definition = new WorkflowDefinition();

        // Assert
        definition.Steps.Should().NotBeNull();
        definition.Steps.Should().BeEmpty();
        definition.Edges.Should().NotBeNull();
        definition.Edges.Should().BeEmpty();
    }
}

[Trait("SDK Unit Tests", "WorkflowStepDefinition JSON model unit tests.")]
public class WorkflowStepDefinitionTests
{
    [Fact]
    public void WorkflowStepDefinition_ShouldSetAndGetProperties()
    {
        // Arrange
        var stepDef = new WorkflowStepDefinition();
        var id = Guid.CreateVersion7();
        var nodeType = WorkflowNodeType.Condition;
        var jobId = Guid.CreateVersion7();
        var stepName = "Test Step";
        var order = 5;
        var nodeConfigJson = "{\"expression\":\"@status == 'Completed'\"}";
        var dataMappings = "{\"sourceStepId:jsonPath\":\"targetJsonPath\"}";
        var delaySeconds = 10;
        var jobDataOverride = "{\"param\":\"value\"}";
        var positionX = 100.5;
        var positionY = 200.5;

        // Act
        stepDef.Id = id;
        stepDef.NodeType = nodeType;
        stepDef.JobId = jobId;
        stepDef.StepName = stepName;
        stepDef.Order = order;
        stepDef.NodeConfigJson = nodeConfigJson;
        stepDef.DataMappings = dataMappings;
        stepDef.DelaySeconds = delaySeconds;
        stepDef.JobDataOverride = jobDataOverride;
        stepDef.PositionX = positionX;
        stepDef.PositionY = positionY;

        // Assert
        stepDef.Id.Should().Be(id);
        stepDef.NodeType.Should().Be(nodeType);
        stepDef.JobId.Should().Be(jobId);
        stepDef.StepName.Should().Be(stepName);
        stepDef.Order.Should().Be(order);
        stepDef.NodeConfigJson.Should().Be(nodeConfigJson);
        stepDef.DataMappings.Should().Be(dataMappings);
        stepDef.DelaySeconds.Should().Be(delaySeconds);
        stepDef.JobDataOverride.Should().Be(jobDataOverride);
        stepDef.PositionX.Should().Be(positionX);
        stepDef.PositionY.Should().Be(positionY);
    }

    [Fact]
    public void WorkflowStepDefinition_ShouldHaveDefaultValues()
    {
        // Act
        var stepDef = new WorkflowStepDefinition();

        // Assert
        stepDef.NodeType.Should().Be(WorkflowNodeType.Task);
        stepDef.DelaySeconds.Should().Be(0);
    }
}

[Trait("SDK Unit Tests", "WorkflowEdgeDefinition JSON model unit tests.")]
public class WorkflowEdgeDefinitionTests
{
    [Fact]
    public void WorkflowEdgeDefinition_ShouldSetAndGetProperties()
    {
        // Arrange
        var edgeDef = new WorkflowEdgeDefinition();
        var sourceStepId = Guid.CreateVersion7();
        var targetStepId = Guid.CreateVersion7();
        var sourcePort = "true";
        var targetPort = "input";
        var label = "Success";
        var order = 3;
        var edgeConfigJson = "{\"color\":\"green\"}";

        // Act
        edgeDef.SourceStepId = sourceStepId;
        edgeDef.TargetStepId = targetStepId;
        edgeDef.SourcePort = sourcePort;
        edgeDef.TargetPort = targetPort;
        edgeDef.Label = label;
        edgeDef.Order = order;
        edgeDef.EdgeConfigJson = edgeConfigJson;

        // Assert
        edgeDef.SourceStepId.Should().Be(sourceStepId);
        edgeDef.TargetStepId.Should().Be(targetStepId);
        edgeDef.SourcePort.Should().Be(sourcePort);
        edgeDef.TargetPort.Should().Be(targetPort);
        edgeDef.Label.Should().Be(label);
        edgeDef.Order.Should().Be(order);
        edgeDef.EdgeConfigJson.Should().Be(edgeConfigJson);
    }
}

[Trait("SDK Unit Tests", "WorkflowSnapshot JSON model unit tests.")]
public class WorkflowSnapshotTests
{
    [Fact]
    public void WorkflowSnapshot_ShouldSetAndGetProperties()
    {
        // Arrange
        var snapshot = new WorkflowSnapshot();
        var id = Guid.CreateVersion7();
        var name = "Snapshot Workflow";
        var description = "Test Description";
        var tags = "tag1,tag2";
        var isActive = true;
        var failureStrategy = WorkflowFailureStrategy.ContinueOnFailure;
        var maxStepRetries = 5;
        var timeoutSeconds = 7200;
        var version = 3;
        var cronExpression = "0 0 12 * * *";
        var lastScheduledRunAt = DateTime.UtcNow;
        var creationDate = DateTime.UtcNow;
        var creatorUserName = "admin";
        var lastModificationDate = DateTime.UtcNow.AddDays(1);
        var lastModifierUserName = "user1";
        var steps = new List<WorkflowStepSnapshot>();
        var edges = new List<WorkflowEdgeSnapshot>();

        // Act
        snapshot.Id = id;
        snapshot.Name = name;
        snapshot.Description = description;
        snapshot.Tags = tags;
        snapshot.IsActive = isActive;
        snapshot.FailureStrategy = failureStrategy;
        snapshot.MaxStepRetries = maxStepRetries;
        snapshot.TimeoutSeconds = timeoutSeconds;
        snapshot.Version = version;
        snapshot.CronExpression = cronExpression;
        snapshot.LastScheduledRunAt = lastScheduledRunAt;
        snapshot.CreationDate = creationDate;
        snapshot.CreatorUserName = creatorUserName;
        snapshot.LastModificationDate = lastModificationDate;
        snapshot.LastModifierUserName = lastModifierUserName;
        snapshot.Steps = steps;
        snapshot.Edges = edges;

        // Assert
        snapshot.Id.Should().Be(id);
        snapshot.Name.Should().Be(name);
        snapshot.Description.Should().Be(description);
        snapshot.Tags.Should().Be(tags);
        snapshot.IsActive.Should().Be(isActive);
        snapshot.FailureStrategy.Should().Be(failureStrategy);
        snapshot.MaxStepRetries.Should().Be(maxStepRetries);
        snapshot.TimeoutSeconds.Should().Be(timeoutSeconds);
        snapshot.Version.Should().Be(version);
        snapshot.CronExpression.Should().Be(cronExpression);
        snapshot.LastScheduledRunAt.Should().Be(lastScheduledRunAt);
        snapshot.CreationDate.Should().Be(creationDate);
        snapshot.CreatorUserName.Should().Be(creatorUserName);
        snapshot.LastModificationDate.Should().Be(lastModificationDate);
        snapshot.LastModifierUserName.Should().Be(lastModifierUserName);
        snapshot.Steps.Should().BeEquivalentTo(steps);
        snapshot.Edges.Should().BeEquivalentTo(edges);
    }
}
