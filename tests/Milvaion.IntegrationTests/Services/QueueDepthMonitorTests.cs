using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Utils.Enums;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for QueueDepthMonitor.
/// Tests queue depth monitoring and health status determination against real RabbitMQ.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class QueueDepthMonitorTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task GetQueueDepthAsync_ShouldReturnQueueInfo_WhenQueueExists()
    {
        // Arrange
        await InitializeAsync();
        await EnsureQueueExistsAsync(WorkerConstant.Queues.StatusUpdates);

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var info = await monitor.GetQueueDepthAsync(WorkerConstant.Queues.StatusUpdates);

        // Assert
        info.Should().NotBeNull();
        info.QueueName.Should().Be(WorkerConstant.Queues.StatusUpdates);
        info.HealthStatus.Should().Be(QueueHealthStatus.Healthy);
    }

    [Fact]
    public async Task GetQueueDepthAsync_ShouldReturnUnavailable_WhenQueueDoesNotExist()
    {
        // Arrange
        await InitializeAsync();

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var info = await monitor.GetQueueDepthAsync("non_existent_queue_" + Guid.CreateVersion7().ToString("N"));

        // Assert
        info.Should().NotBeNull();
        info.HealthStatus.Should().Be(QueueHealthStatus.Unavailable);
    }

    [Fact]
    public async Task IsQueueHealthyAsync_ShouldReturnTrue_WhenQueueIsHealthy()
    {
        // Arrange
        await InitializeAsync();
        await EnsureQueueExistsAsync(WorkerConstant.Queues.WorkerLogs);

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var isHealthy = await monitor.IsQueueHealthyAsync(WorkerConstant.Queues.WorkerLogs);

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task IsQueueHealthyAsync_ShouldReturnFalse_WhenQueueDoesNotExist()
    {
        // Arrange
        await InitializeAsync();

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var isHealthy = await monitor.IsQueueHealthyAsync("non_existent_queue_" + Guid.CreateVersion7().ToString("N"));

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task GetQueueDepthAsync_ShouldReturnCorrectMessageCount()
    {
        // Arrange
        await InitializeAsync();
        var queueName = $"test-depth-{Guid.CreateVersion7():N}";
        await EnsureQueueExistsAsync(queueName);

        // Publish some messages
        await PublishTestMessagesAsync(queueName, 3);

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var info = await monitor.GetQueueDepthAsync(queueName);

        // Assert
        info.MessageCount.Should().Be(3);
    }

    [Fact]
    public async Task GetDetailedStatsAsync_ShouldReturnDetailedInfo()
    {
        // Arrange
        await InitializeAsync();
        await EnsureQueueExistsAsync(WorkerConstant.Queues.StatusUpdates);

        var monitor = _serviceProvider.GetRequiredService<IQueueDepthMonitor>();

        // Act
        var stats = await monitor.GetDetailedStatsAsync(WorkerConstant.Queues.StatusUpdates);

        // Assert
        stats.Should().NotBeNull();
        stats.QueueName.Should().Be(WorkerConstant.Queues.StatusUpdates);
        stats.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    private async Task EnsureQueueExistsAsync(string queueName)
    {
        var connectionFactory = _serviceProvider.GetRequiredService<RabbitMQConnectionFactory>();
        await using var channel = await connectionFactory.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    private async Task PublishTestMessagesAsync(string queueName, int count)
    {
        var rabbitFactory = new ConnectionFactory
        {
            HostName = _factory.GetRabbitMqHost(),
            Port = _factory.GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await rabbitFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        for (int i = 0; i < count; i++)
        {
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                body: System.Text.Encoding.UTF8.GetBytes($"test-message-{i}"));
        }

        // Give RabbitMQ time to process
        await Task.Delay(500);
    }
}
