using FluentAssertions;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Models;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisWorkerService.
/// Tests worker registration, heartbeat, and retrieval operations against real Redis.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class RedisWorkerServiceTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task RegisterWorkerAsync_ShouldRegisterNewWorker()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();
        var registration = CreateTestRegistration("test-worker-01", "instance-01");

        // Act
        var result = await workerService.RegisterWorkerAsync(registration);

        // Assert
        result.Should().BeTrue();

        var workers = await workerService.GetAllWorkersAsync();
        workers.Should().Contain(w => w.WorkerId == "test-worker-01");
    }

    [Fact]
    public async Task RegisterWorkerAsync_ShouldUpdateExistingWorker()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();
        var registration = CreateTestRegistration("test-worker-01", "instance-01");

        await workerService.RegisterWorkerAsync(registration);

        // Act - Register again with updated info
        registration.DisplayName = "Updated Worker";
        var result = await workerService.RegisterWorkerAsync(registration);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_ShouldUpdateHeartbeat_WhenInstanceExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();
        var registration = CreateTestRegistration("heartbeat-worker", "instance-01");

        await workerService.RegisterWorkerAsync(registration);

        // Act
        var result = await workerService.UpdateHeartbeatAsync("heartbeat-worker", "instance-01", currentJobs: 3);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_ShouldReturnFalse_WhenInstanceDoesNotExist()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();

        // Act - No registration, try to heartbeat
        var result = await workerService.UpdateHeartbeatAsync("nonexistent-worker", "nonexistent-instance", currentJobs: 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllWorkersAsync_ShouldReturnAllRegisteredWorkers()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();

        await workerService.RegisterWorkerAsync(CreateTestRegistration("worker-01", "instance-01"));
        await workerService.RegisterWorkerAsync(CreateTestRegistration("worker-02", "instance-01"));

        // Act
        var workers = await workerService.GetAllWorkersAsync();

        // Assert
        workers.Should().HaveCountGreaterThanOrEqualTo(2);
        workers.Should().Contain(w => w.WorkerId == "worker-01");
        workers.Should().Contain(w => w.WorkerId == "worker-02");
    }

    [Fact]
    public async Task GetAllWorkersAsync_ShouldReturnEmptyList_WhenNoWorkersRegistered()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();

        // Act
        var workers = await workerService.GetAllWorkersAsync();

        // Assert
        workers.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterWorkerAsync_ShouldSupportMultipleInstances()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var workerService = GetRedisWorkerService();

        await workerService.RegisterWorkerAsync(CreateTestRegistration("multi-instance-worker", "instance-01"));
        await workerService.RegisterWorkerAsync(CreateTestRegistration("multi-instance-worker", "instance-02"));

        // Act
        var workers = await workerService.GetAllWorkersAsync();

        // Assert
        var worker = workers.FirstOrDefault(w => w.WorkerId == "multi-instance-worker");
        worker.Should().NotBeNull();
        worker.Instances.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    private static WorkerDiscoveryRequest CreateTestRegistration(string workerId, string instanceId) => new()
    {
        WorkerId = workerId,
        InstanceId = instanceId,
        DisplayName = $"Test {workerId}",
        HostName = "test-host",
        IpAddress = "127.0.0.1",
        Version = "1.0.0",
        MaxParallelJobs = 5,
        JobTypes = ["TestJob"],
        RoutingPatterns = new Dictionary<string, string> { [$"worker.{workerId}"] = $"worker.{workerId}" },
        Metadata = "{}",
        JobDataDefinitions = []
    };
}
