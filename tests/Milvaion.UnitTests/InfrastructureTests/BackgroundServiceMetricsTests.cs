using FluentAssertions;
using Milvaion.Infrastructure.Telemetry;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for BackgroundServiceMetrics.
/// Tests OpenTelemetry metrics recording for background services.
/// </summary>
public class BackgroundServiceMetricsTests
{
    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        // Act
        var act = () => new BackgroundServiceMetrics();

        // Assert
        act.Should().NotThrow();
    }

    #region Job Dispatcher Metrics

    [Fact]
    public void RecordJobDispatched_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordJobDispatched("TestJob", "high");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordJobsDispatched_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordJobsDispatched(10, "TestJob");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordDispatchFailure_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordDispatchFailure("connection_error");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordDispatchDuration_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordDispatchDuration(150.5, 10);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetPendingJobsCount_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.SetPendingJobsCount(25);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Status Tracker Metrics

    [Fact]
    public void RecordStatusUpdatesProcessed_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordStatusUpdatesProcessed(50);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordStatusUpdateByStatus_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordStatusUpdateByStatus("Completed");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordStatusUpdateFailure_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordStatusUpdateFailure("db_timeout");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordStatusUpdateDuration_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordStatusUpdateDuration(200.0, 25);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetStatusBatchSize_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.SetStatusBatchSize(15);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Log Collector Metrics

    [Fact]
    public void RecordLogsCollected_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordLogsCollected(100, "Information");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordLogCollectionFailure_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordLogCollectionFailure("queue_full");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordLogBatchDuration_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordLogBatchDuration(50.0, 20);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetLogBatchSize_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.SetLogBatchSize(30);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Worker Discovery Metrics

    [Fact]
    public void RecordWorkerRegistration_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordWorkerRegistration("email-worker");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordWorkerHeartbeats_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordWorkerHeartbeats(5);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordHeartbeatFailure_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordHeartbeatFailure("timeout");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetActiveWorkersCount_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.SetActiveWorkersCount(10);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Zombie Detector Metrics

    [Fact]
    public void RecordZombiesDetected_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.RecordZombiesDetected(2);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllMethods_WithZeroValues_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act & Assert
        var act = () =>
        {
            metrics.RecordJobsDispatched(0);
            metrics.RecordDispatchDuration(0, 0);
            metrics.RecordStatusUpdatesProcessed(0);
            metrics.RecordStatusUpdateDuration(0, 0);
            metrics.RecordLogsCollected(0);
            metrics.RecordLogBatchDuration(0, 0);
            metrics.RecordWorkerHeartbeats(0);
            metrics.RecordZombiesDetected(0);
            metrics.SetPendingJobsCount(0);
            metrics.SetStatusBatchSize(0);
            metrics.SetLogBatchSize(0);
            metrics.SetActiveWorkersCount(0);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AllMethods_WithLargeValues_ShouldNotThrow()
    {
        // Arrange
        using var metrics = new BackgroundServiceMetrics();

        // Act & Assert
        var act = () =>
        {
            metrics.RecordJobsDispatched(int.MaxValue);
            metrics.RecordDispatchDuration(double.MaxValue / 2, int.MaxValue);
            metrics.RecordStatusUpdatesProcessed(int.MaxValue);
            metrics.RecordStatusUpdateDuration(double.MaxValue / 2, int.MaxValue);
            metrics.RecordLogsCollected(int.MaxValue);
            metrics.RecordLogBatchDuration(double.MaxValue / 2, int.MaxValue);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var metrics = new BackgroundServiceMetrics();

        // Act
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var metrics = new BackgroundServiceMetrics();

        // Act
        metrics.Dispose();
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
