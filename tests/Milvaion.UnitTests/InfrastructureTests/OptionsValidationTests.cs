using FluentAssertions;
using Milvaion.Application.Utils.Models.Options;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for configuration options classes.
/// Tests default values and property assignments.
/// </summary>
#pragma warning disable IDE0022 // Use expression body for method
public class OptionsValidationTests
{
    #region StatusTrackerOptions

    [Fact]
    public void StatusTrackerOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new StatusTrackerOptions();

        // Assert
        options.BatchSize.Should().Be(50);
        options.BatchIntervalMs.Should().Be(500);
        options.ExecutionLogMaxCount.Should().Be(100);
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void StatusTrackerOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        StatusTrackerOptions.SectionKey.Should().Be("MilvaionConfig:StatusTracker");
    }

    [Fact]
    public void StatusTrackerOptions_PropertyAssignment_ShouldWork()
    {
        // Arrange
        var options = new StatusTrackerOptions
        {
            BatchSize = 100,
            BatchIntervalMs = 1000,
            ExecutionLogMaxCount = 50,
            Enabled = false
        };

        // Assert
        options.BatchSize.Should().Be(100);
        options.BatchIntervalMs.Should().Be(1000);
        options.ExecutionLogMaxCount.Should().Be(50);
        options.Enabled.Should().BeFalse();
    }

    #endregion

    #region JobDispatcherOptions

    [Fact]
    public void JobDispatcherOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new JobDispatcherOptions();

        // Assert
        options.PollingIntervalSeconds.Should().Be(10);
        options.BatchSize.Should().Be(100);
        options.LockTtlSeconds.Should().Be(600);
        options.EnableStartupRecovery.Should().BeTrue();
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void JobDispatcherOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        JobDispatcherOptions.SectionKey.Should().Be("MilvaionConfig:JobDispatcher");
    }

    [Fact]
    public void JobDispatcherOptions_PropertyAssignment_ShouldWork()
    {
        // Arrange
        var options = new JobDispatcherOptions
        {
            PollingIntervalSeconds = 5,
            BatchSize = 50,
            LockTtlSeconds = 300,
            EnableStartupRecovery = false,
            Enabled = false
        };

        // Assert
        options.PollingIntervalSeconds.Should().Be(5);
        options.BatchSize.Should().Be(50);
        options.LockTtlSeconds.Should().Be(300);
        options.EnableStartupRecovery.Should().BeFalse();
        options.Enabled.Should().BeFalse();
    }

    #endregion

    #region RabbitMQOptions

    [Fact]
    public void RabbitMQOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new RabbitMQOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(5672);
        options.Username.Should().BeNull();
        options.Password.Should().BeNull();
        options.VirtualHost.Should().Be("/");
        options.Durable.Should().BeTrue();
        options.AutoDelete.Should().BeFalse();
    }

    [Fact]
    public void RabbitMQOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        RabbitMQOptions.SectionKey.Should().Be("MilvaionConfig:RabbitMQ");
    }

    #endregion

    #region RedisOptions

    [Fact]
    public void RedisOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new RedisOptions();

        // Assert
        options.ConnectionString.Should().Be("localhost:6379");
        options.Database.Should().Be(0);
        options.KeyPrefix.Should().Be("Milvaion:JobScheduler:");
    }

    [Fact]
    public void RedisOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        RedisOptions.SectionKey.Should().Be("MilvaionConfig:Redis");
    }

    #endregion

    #region LogCollectorOptions

    [Fact]
    public void LogCollectorOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new LogCollectorOptions();

        // Assert
        options.BatchSize.Should().Be(100);
        options.BatchIntervalMs.Should().Be(1000);
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void LogCollectorOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        LogCollectorOptions.SectionKey.Should().Be("MilvaionConfig:LogCollector");
    }

    #endregion

    #region ZombieDetectorOptions

    [Fact]
    public void ZombieDetectorOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new ZombieOccurrenceDetectorOptions();

        // Assert
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ZombieDetectorOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        ZombieOccurrenceDetectorOptions.SectionKey.Should().Be("MilvaionConfig:ZombieOccurrenceDetector");
    }

    #endregion

    #region FailedOccurrenceHandlerOptions

    [Fact]
    public void FailedOccurrenceHandlerOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new FailedOccurrenceHandlerOptions();

        // Assert
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void FailedOccurrenceHandlerOptions_SectionKey_ShouldBeCorrect()
    {
        // Assert
        FailedOccurrenceHandlerOptions.SectionKey.Should().Be("MilvaionConfig:FailedOccurrenceHandler");
    }

    #endregion
}
#pragma warning restore IDE0022 // Use expression body for method
