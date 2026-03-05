using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Worker.HealthChecks;
using Milvasoft.Milvaion.Sdk.Worker.Persistence;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for worker health checks against real Redis and RabbitMQ.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class WorkerHealthCheckTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    #region RedisHealthCheck

    [Fact]
    public async Task RedisHealthCheck_ShouldReturnHealthy_WhenRedisIsConnected()
    {
        // Arrange

        var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionString());
        var healthCheck = new RedisHealthCheck(redis);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("Redis", healthCheck, null, null)
        });

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("ConnectionStatus");
        result.Data["ConnectionStatus"].Should().Be("Connected");
        result.Data.Should().ContainKey("LatencyMs");
    }

    [Fact]
    public async Task RedisHealthCheck_ShouldReturnUnhealthy_WhenRedisIsNull()
    {
        // Arrange

        var healthCheck = new RedisHealthCheck(null);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("Redis", healthCheck, null, null)
        });

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().ContainKey("ConnectionStatus");
        result.Data["ConnectionStatus"].Should().Be("Disconnected");
    }

    #endregion

    #region RabbitMQHealthCheck

    [Fact]
    public async Task RabbitMQHealthCheck_ShouldReturnHealthy_WhenRabbitMQIsConnected()
    {
        // Arrange

        var connectionMonitor = new StubConnectionMonitor(isHealthy: true);
        var healthCheck = new RabbitMQHealthCheck(connectionMonitor);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("RabbitMQ", healthCheck, null, null)
        });

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("ConnectionStatus");
        result.Data["ConnectionStatus"].Should().Be("Connected");
    }

    [Fact]
    public async Task RabbitMQHealthCheck_ShouldReturnUnhealthy_WhenRabbitMQIsDisconnected()
    {
        // Arrange

        var connectionMonitor = new StubConnectionMonitor(isHealthy: false);
        var healthCheck = new RabbitMQHealthCheck(connectionMonitor);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("RabbitMQ", healthCheck, null, null)
        });

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data.Should().ContainKey("ConnectionStatus");
        result.Data["ConnectionStatus"].Should().Be("Disconnected");
    }

    #endregion

    #region Stubs

    private sealed class StubConnectionMonitor(bool isHealthy) : IConnectionMonitor
    {
        public bool IsRabbitMQHealthy => isHealthy;
        public bool IsRedisHealthy => isHealthy;

        public Task<bool> RefreshStatusAsync() => Task.FromResult(isHealthy);
        public void OnConnectionRestored() { }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    #endregion
}
