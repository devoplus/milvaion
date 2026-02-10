using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Moq;
using Quartz;
using Quartz.Impl;

namespace Milvaion.UnitTests.QuartzSdkTests;

[Trait("Quartz SDK Unit Tests", "MilvaionJobListener unit tests.")]
public class MilvaionJobListenerTests
{
    private readonly Mock<IExternalJobPublisher> _publisherMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly MilvaionJobListener _listener;

    public MilvaionJobListenerTests()
    {
        _publisherMock = new Mock<IExternalJobPublisher>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        var workerOptions = Options.Create(new WorkerOptions
        {
            WorkerId = "quartz-test-worker",
            ExternalScheduler = new MilvaionExternalSchedulerOptions
            {
                Source = "Quartz"
            }
        });

        _listener = new MilvaionJobListener(_publisherMock.Object, workerOptions, _loggerFactoryMock.Object);
    }

    [Fact]
    public void Name_ShouldReturnMilvaionJobListener()
    {
        _listener.Name.Should().Be("MilvaionJobListener");
    }

    [Fact]
    public async Task JobToBeExecuted_ShouldPublishStartingEvent()
    {
        // Arrange
        ExternalJobOccurrenceMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobOccurrenceMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");

        // Act
        await _listener.JobToBeExecuted(context);

        // Assert
        _publisherMock.Verify(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedMessage.Should().NotBeNull();
        capturedMessage.Status.Should().Be(JobOccurrenceStatus.Running);
        capturedMessage.EventType.Should().Be(ExternalOccurrenceEventType.Starting);
        capturedMessage.Source.Should().Be("Quartz");
        capturedMessage.WorkerId.Should().Be("quartz-test-worker");
        capturedMessage.ExternalJobId.Should().Contain("TestJob");
    }

    [Fact]
    public async Task JobToBeExecuted_ShouldStoreCorrelationIdInJobDataMap()
    {
        // Arrange
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");

        // Act
        await _listener.JobToBeExecuted(context);

        // Assert
        context.MergedJobDataMap.GetString("Milvaion_CorrelationId").Should().NotBeNullOrEmpty();
        context.MergedJobDataMap.GetString("Milvaion_WorkerId").Should().Be("quartz-test-worker");
    }

    [Fact]
    public async Task JobWasExecuted_WithoutException_ShouldPublishCompletedStatus()
    {
        // Arrange
        ExternalJobOccurrenceMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobOccurrenceMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");
        context.MergedJobDataMap.Put("Milvaion_CorrelationId", Guid.CreateVersion7().ToString());

        // Act
        await _listener.JobWasExecuted(context, null);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Status.Should().Be(JobOccurrenceStatus.Completed);
        capturedMessage.EventType.Should().Be(ExternalOccurrenceEventType.Completed);
        capturedMessage.Exception.Should().BeNull();
    }

    [Fact]
    public async Task JobWasExecuted_WithException_ShouldPublishFailedStatus()
    {
        // Arrange
        ExternalJobOccurrenceMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobOccurrenceMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");
        context.MergedJobDataMap.Put("Milvaion_CorrelationId", Guid.CreateVersion7().ToString());
        var jobException = new JobExecutionException("Test job failed");

        // Act
        await _listener.JobWasExecuted(context, jobException);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Status.Should().Be(JobOccurrenceStatus.Failed);
        capturedMessage.Exception.Should().Contain("Test job failed");
    }

    [Fact]
    public async Task JobExecutionVetoed_ShouldPublishCancelledStatus()
    {
        // Arrange
        ExternalJobOccurrenceMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobOccurrenceMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");

        // Act
        await _listener.JobExecutionVetoed(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Status.Should().Be(JobOccurrenceStatus.Cancelled);
        capturedMessage.EventType.Should().Be(ExternalOccurrenceEventType.Vetoed);
        capturedMessage.Result.Should().Contain("vetoed");
    }

    [Fact]
    public async Task JobToBeExecuted_ShouldNotThrow_WhenPublisherFails()
    {
        // Arrange
        _publisherMock.Setup(x => x.PublishOccurrenceEventAsync(It.IsAny<ExternalJobOccurrenceMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("RabbitMQ connection failed"));

        var context = CreateJobExecutionContext("TestJob", "DEFAULT");

        // Act & Assert - should not throw
        var act = async () => await _listener.JobToBeExecuted(context);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task JobToBeExecuted_ShouldNotThrow_WhenPublisherIsNull()
    {
        // Arrange
        var workerOptions = Options.Create(new WorkerOptions
        {
            ExternalScheduler = new MilvaionExternalSchedulerOptions { Source = "Quartz" }
        });

        var listener = new MilvaionJobListener(null, workerOptions, _loggerFactoryMock.Object);
        var context = CreateJobExecutionContext("TestJob", "DEFAULT");

        // Act & Assert
        var act = async () => await listener.JobToBeExecuted(context);
        await act.Should().NotThrowAsync();
    }

    private static IJobExecutionContext CreateJobExecutionContext(string jobName, string groupName)
    {
        var jobDetail = JobBuilder.Create<DummyQuartzJob>()
            .WithIdentity(jobName, groupName)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobName}-trigger", groupName)
            .StartNow()
            .Build();

        var context = new Mock<IJobExecutionContext>();
        context.Setup(x => x.JobDetail).Returns(jobDetail);
        context.Setup(x => x.Trigger).Returns(trigger);
        context.Setup(x => x.FireInstanceId).Returns(Guid.CreateVersion7().ToString());
        context.Setup(x => x.ScheduledFireTimeUtc).Returns(DateTimeOffset.UtcNow);
        context.Setup(x => x.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        context.Setup(x => x.JobRunTime).Returns(TimeSpan.FromMilliseconds(1500));
        context.Setup(x => x.MergedJobDataMap).Returns(new JobDataMap());

        return context.Object;
    }

    private sealed class DummyQuartzJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
