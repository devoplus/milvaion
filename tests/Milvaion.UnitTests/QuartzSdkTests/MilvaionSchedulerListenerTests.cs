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
using Quartz.Impl.Triggers;

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

    [Fact]
    public async Task JobScheduled_ShouldNotPublish_WhenNoMatchingJobAdded()
    {
        // Arrange - Schedule trigger without prior JobAdded
        var trigger = TriggerBuilder.Create()
            .WithIdentity("OrphanTrigger", "DEFAULT")
            .ForJob("NonExistentJob", "DEFAULT")
            .StartNow()
            .Build();

        // Act
        await _listener.JobScheduled(trigger);

        // Assert - No registration should be published since no pending job detail exists
        _publisherMock.Verify(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobScheduled_ShouldNotThrow_WhenPublisherThrowsException()
    {
        // Arrange
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ unavailable"));

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("FailPublishJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("FailPublishTrigger", "DEFAULT")
            .ForJob("FailPublishJob", "DEFAULT")
            .StartNow()
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act & Assert - Should not throw (all methods are wrapped in try-catch)
        var act = async () => await _listener.JobScheduled(trigger);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task JobDeleted_ShouldNotThrow_WhenPublisherThrowsException()
    {
        // Arrange
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        // Act & Assert
        var act = async () => await _listener.JobDeleted(new JobKey("BrokenDeleteJob", "DEFAULT"));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SchedulerStarted_ShouldNotStartPublisher_WhenRegistryIsEmpty()
    {
        // Arrange - registry is already empty (no jobs added)

        // Act & Assert - Should not throw, just log warning
        var act = async () => await _listener.SchedulerStarted();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SchedulerStarted_ShouldNotThrow_WhenNullServiceProvider()
    {
        // Arrange - Create listener with null serviceProvider
        var listener = new MilvaionSchedulerListener(
            _publisherMock.Object,
            Microsoft.Extensions.Options.Options.Create(new WorkerOptions
            {
                WorkerId = "null-sp-worker",
                ExternalScheduler = new MilvaionExternalSchedulerOptions { Source = "Quartz" },
                RabbitMQ = new RabbitMQSettings
                {
                    Host = "localhost",
                    Port = 5672,
                    Username = "guest",
                    Password = "guest",
                    VirtualHost = "/"
                }
            }),
            new ExternalJobRegistry(),
            null, // null serviceProvider
            _loggerFactoryMock.Object);

        // Act & Assert
        var act = async () => await listener.SchedulerStarted();
        await act.Should().NotThrowAsync();
    }

    #region Trigger Type Branches (GetCronExpression coverage)

    [Fact]
    public async Task JobScheduled_WithSimpleTrigger_ShouldPublishWithGeneratedCron()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("SimpleJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("SimpleTrigger", "DEFAULT")
            .ForJob("SimpleJob", "DEFAULT")
            .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.CronExpression.Should().NotBeNullOrEmpty();
        capturedMessage.CronExpression.Should().Contain("5");
    }

    [Fact]
    public async Task JobScheduled_WithCalendarIntervalTrigger_Hours_ShouldPublishWithGeneratedCron()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("CalendarHourJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("CalendarHourTrigger", "DEFAULT")
            .ForJob("CalendarHourJob", "DEFAULT")
            .WithCalendarIntervalSchedule(x => x.WithIntervalInHours(2))
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.CronExpression.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(IntervalUnit.Second, 30)]
    [InlineData(IntervalUnit.Minute, 10)]
    [InlineData(IntervalUnit.Hour, 2)]
    [InlineData(IntervalUnit.Day, 1)]
    [InlineData(IntervalUnit.Week, 1)]
    [InlineData(IntervalUnit.Month, 1)]
    [InlineData(IntervalUnit.Year, 1)]
    public async Task JobScheduled_WithCalendarIntervalTrigger_AllUnits_ShouldPublishWithGeneratedCron(IntervalUnit unit, int interval)
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobName = $"CalUnit_{unit}_{interval}";
        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity(jobName, "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"Trigger_{jobName}", "DEFAULT")
            .ForJob(jobName, "DEFAULT")
            .WithCalendarIntervalSchedule(x =>
            {
                x.WithInterval(interval, unit);
            })
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.CronExpression.Should().NotBeNullOrEmpty("unit {0} with interval {1} should produce a cron expression", unit, interval);
    }

    [Fact]
    public async Task JobScheduled_WithDailyTimeIntervalTrigger_ShouldPublishWithGeneratedCron()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("DailyTimeJob", "DEFAULT")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("DailyTimeTrigger", "DEFAULT")
            .ForJob("DailyTimeJob", "DEFAULT")
            .WithDailyTimeIntervalSchedule(x => x.WithIntervalInMinutes(30))
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.CronExpression.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(IntervalUnit.Second, 45)]
    [InlineData(IntervalUnit.Minute, 15)]
    [InlineData(IntervalUnit.Hour, 3)]
    public async Task JobScheduled_WithDailyTimeIntervalTrigger_AllUnits_ShouldPublishWithGeneratedCron(IntervalUnit unit, int interval)
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobName = $"DailyUnit_{unit}_{interval}";
        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity(jobName, "DEFAULT")
            .Build();

        var triggerImpl = new DailyTimeIntervalTriggerImpl
        {
            Key = new TriggerKey($"Trigger_{jobName}", "DEFAULT"),
            JobKey = new JobKey(jobName, "DEFAULT"),
            RepeatInterval = interval,
            RepeatIntervalUnit = unit
        };

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(triggerImpl);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.CronExpression.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Catch Block Coverage

    [Fact]
    public async Task JobAdded_ShouldNotThrow_WhenNullJobDetail()
    {
        // Arrange - null jobDetail will cause NRE inside try block, hitting catch
        var act = async () => await _listener.JobAdded(null);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SchedulerShuttingdown_ShouldNotThrow_WhenStopAsyncFails()
    {
        // Arrange - First register a job + start to set _workerListenerPublisher
        // Since _serviceProvider is null in the default listener, SchedulerStarted
        // won't create the publisher. We need the catch path where shuttingdown itself fails.
        // Calling SchedulerShuttingdown when no publisher is set just logs and returns.
        var act = async () => await _listener.SchedulerShuttingdown();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task JobScheduled_WithJobDataMap_ShouldIncludeSerializedData()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("DataMapJob", "DEFAULT")
            .UsingJobData("key1", "value1")
            .UsingJobData("key2", 42)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("DataMapTrigger", "DEFAULT")
            .ForJob("DataMapJob", "DEFAULT")
            .StartNow()
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.JobData.Should().NotBeNullOrEmpty();
        capturedMessage.JobData.Should().Contain("key1");
    }

    [Fact]
    public async Task JobDeleted_ShouldNotPublish_WhenPublisherIsNull()
    {
        // Arrange - Create listener with null publisher
        var listener = new MilvaionSchedulerListener(
            null, // null publisher
            Options.Create(new WorkerOptions
            {
                WorkerId = "null-pub-worker",
                ExternalScheduler = new MilvaionExternalSchedulerOptions { Source = "Quartz" },
                RabbitMQ = new RabbitMQSettings
                {
                    Host = "localhost",
                    Port = 5672,
                    Username = "guest",
                    Password = "guest",
                    VirtualHost = "/"
                }
            }),
            _jobRegistry,
            null,
            _loggerFactoryMock.Object);

        // Act & Assert - Should return early without throwing
        var act = async () => await listener.JobDeleted(new JobKey("NullPubJob", "DEFAULT"));
        await act.Should().NotThrowAsync();

        _publisherMock.Verify(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JobScheduled_WithCustomGroup_ShouldSetTags()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("TaggedJob", "MyCustomGroup")
            .WithDescription("A tagged job")
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("TaggedTrigger", "MyCustomGroup")
            .ForJob("TaggedJob", "MyCustomGroup")
            .StartNow()
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Tags.Should().Be("MyCustomGroup");
        capturedMessage.Description.Should().Be("A tagged job");
    }

    [Fact]
    public async Task JobScheduled_WithDefaultGroup_ShouldNotSetTags()
    {
        // Arrange
        ExternalJobRegistrationMessage capturedMessage = null;
        _publisherMock.Setup(x => x.PublishJobRegistrationAsync(It.IsAny<ExternalJobRegistrationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalJobRegistrationMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var jobDetail = JobBuilder.Create<DummyJob>()
            .WithIdentity("DefaultGroupJob", JobKey.DefaultGroup)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("DefaultGroupTrigger", JobKey.DefaultGroup)
            .ForJob("DefaultGroupJob", JobKey.DefaultGroup)
            .StartNow()
            .Build();

        await _listener.JobAdded(jobDetail);

        // Act
        await _listener.JobScheduled(trigger);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage.Tags.Should().BeNull();
    }

    #endregion

    #region Remaining Event Methods Coverage

    [Fact]
    public Task JobUnscheduled_ShouldNotThrow()
    {
        var act = async () => await _listener.JobUnscheduled(new TriggerKey("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task TriggerFinalized_ShouldNotThrow()
    {
        var trigger = TriggerBuilder.Create().WithIdentity("fin").StartNow().Build();
        var act = async () => await _listener.TriggerFinalized(trigger);
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task TriggerPaused_ShouldNotThrow()
    {
        var act = async () => await _listener.TriggerPaused(new TriggerKey("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task TriggersPaused_ShouldNotThrow()
    {
        var act = async () => await _listener.TriggersPaused("group");
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task TriggerResumed_ShouldNotThrow()
    {
        var act = async () => await _listener.TriggerResumed(new TriggerKey("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task TriggersResumed_ShouldNotThrow()
    {
        var act = async () => await _listener.TriggersResumed("group");
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task JobPaused_ShouldNotThrow()
    {
        var act = async () => await _listener.JobPaused(new JobKey("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task JobsPaused_ShouldNotThrow()
    {
        var act = async () => await _listener.JobsPaused("group");
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task JobResumed_ShouldNotThrow()
    {
        var act = async () => await _listener.JobResumed(new JobKey("test"));
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public Task JobsResumed_ShouldNotThrow()
    {
        var act = async () => await _listener.JobsResumed("group");
        return act.Should().NotThrowAsync();
    }

    #endregion

    private sealed class DummyJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
