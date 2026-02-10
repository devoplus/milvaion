using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using Moq;
using Quartz;

namespace Milvaion.UnitTests.QuartzSdkTests;

[Trait("Quartz SDK Unit Tests", "MilvaionSchedulerListener unit tests.")]
public class MilvaionSchedulerListenerTests
{
    private readonly Mock<IExternalJobPublisher> _publisherMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly ExternalJobRegistry _jobRegistry;
    private readonly MilvaionSchedulerListener _listener;

    public MilvaionSchedulerListenerTests()
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
            },
            RabbitMQ = new RabbitMQSettings
            {
                Host = "localhost",
                Port = 5672,
                Username = "guest",
                Password = "guest",
                VirtualHost = "/"
            }
        });

        _jobRegistry = new ExternalJobRegistry();

        _listener = new MilvaionSchedulerListener(
            _publisherMock.Object,
            workerOptions,
            _jobRegistry,
            null,
            _loggerFactoryMock.Object);
    }

    [Fact]
    public async Task JobAdded_ShouldStorePendingJobDetail()
    {
        // Arrange
        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("TestJob", "DEFAULT")
            .Build();

        // Act
        await _listener.JobAdded(jobDetail);

        // Assert - JobAdded stores the detail, no publish yet
        _publisherMock.Verify(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobScheduled_AfterJobAdded_ShouldPublishRegistration()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("TestJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("TestTrigger", "DEFAULT")
            .ForJob("TestJob", "DEFAULT")
            .WithCronSchedule("0 * * * * ?")
            .Build();

        // First add the job
        await _listener.JobAdded(jobDetail);

        // Act - then schedule it (triggers publish)
        await _listener.JobScheduled(trigger);

        // Assert
        _publisherMock.Verify(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedMessage.Should().NotBeNull();
        capturedMessage.Source.Should().Be("Quartz");
        capturedMessage.WorkerId.Should().Be("quartz-test-worker");
        capturedMessage.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task JobScheduled_AfterJobAdded_ShouldRegisterInJobRegistry()
    {
        // Arrange
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("RegistryJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("RegistryTrigger", "DEFAULT")
            .ForJob("RegistryJob", "DEFAULT")
            .StartNow()
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        _jobRegistry.Count.Should().Be(1);
    }

    [Fact]
    public async Task JobDeleted_ShouldPublishInactiveRegistration()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobKey = new JobKey("DeletedJob", "DEFAULT");

        // Act
        await _listener.JobDeleted(jobKey);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.IsActive.Should().BeFalse();
        capturedMessage.DisplayName.Should().Be("DeletedJob");
    }

    [Fact]
    public Task SchedulerStarting_ShouldNotThrow()
    {
        var act = async () => await _listener.SchedulerStarting();
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task SchedulerShutdown_ShouldNotThrow()
    {
        var act = async () => await _listener.SchedulerShutdown();
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task SchedulerError_ShouldNotThrow()
    {
        var act = async () => await _listener.SchedulerError("Test error", new SchedulerException("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task SchedulerInStandbyMode_ShouldNotThrow()
    {
        var act = async () => await _listener.SchedulerInStandbyMode();
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task SchedulingDataCleared_ShouldNotThrow()
    {
        var act = async () => await _listener.SchedulingDataCleared();
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task JobInterrupted_ShouldNotThrow()
    {
        var act = async () => await _listener.JobInterrupted(new JobKey("test"));
        return act.Should().NotThrowAsync();
    }

    private sealed class DummyJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
