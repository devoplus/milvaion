using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Milvaion.Application.Interfaces.RabbitMQ;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RabbitMQPublisher.
/// Tests job publishing to RabbitMQ exchange against real RabbitMQ.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class RabbitMQPublisherTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task PublishJobAsync_ShouldPublishJobToExchange()
    {
        // Arrange
        await InitializeAsync();
        await EnsureExchangeExistsAsync();

        var publisher = _serviceProvider.GetRequiredService<IRabbitMQPublisher>();
        var job = CreateTestScheduledJob("TestPublishJob", "test-worker-01");
        var correlationId = Guid.CreateVersion7();

        // Bind a temporary queue to receive the message
        var queueName = await BindTemporaryQueueAsync("test-worker-01.testpublish.job");

        // Act
        var result = await publisher.PublishJobAsync(job, correlationId);

        // Assert
        result.Should().BeTrue();

        // Verify message arrived in queue
        await Task.Delay(500);
        var messageCount = await GetQueueMessageCountAsync(queueName);
        messageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PublishJobAsync_ShouldReturnTrue_OnSuccessfulPublish()
    {
        // Arrange
        await InitializeAsync();
        await EnsureExchangeExistsAsync();

        var publisher = _serviceProvider.GetRequiredService<IRabbitMQPublisher>();
        var job = CreateTestScheduledJob("SuccessJob", "test-worker-02");
        var correlationId = Guid.CreateVersion7();

        // Bind a temporary queue so mandatory publish doesn't fail
        await BindTemporaryQueueAsync("test-worker-02.success.job");

        // Act
        var result = await publisher.PublishJobAsync(job, correlationId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishMultipleJobs()
    {
        // Arrange
        await InitializeAsync();
        await EnsureExchangeExistsAsync();

        var publisher = _serviceProvider.GetRequiredService<IRabbitMQPublisher>();
        var workerId = "batch-worker-01";

        var jobsWithCorrelation = new Dictionary<ScheduledJob, Guid>();
        for (int i = 0; i < 3; i++)
        {
            var job = CreateTestScheduledJob($"BatchJob{i}", workerId);
            jobsWithCorrelation[job] = Guid.CreateVersion7();
        }

        // Bind queues for each routing key
        await BindTemporaryQueueAsync($"{workerId}.batchjob0.job");
        await BindTemporaryQueueAsync($"{workerId}.batchjob1.job");
        await BindTemporaryQueueAsync($"{workerId}.batchjob2.job");

        // Act
        var publishedCount = await publisher.PublishBatchAsync(jobsWithCorrelation);

        // Assert
        publishedCount.Should().Be(3);
    }

    [Fact]
    public async Task PublishBatchAsync_WithEmptyDictionary_ShouldReturnZero()
    {
        // Arrange
        await InitializeAsync();

        var publisher = _serviceProvider.GetRequiredService<IRabbitMQPublisher>();

        // Act
        var publishedCount = await publisher.PublishBatchAsync([]);

        // Assert
        publishedCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishJobAsync_WithRoutingPattern_ShouldUseCorrectRoutingKey()
    {
        // Arrange
        await InitializeAsync();
        await EnsureExchangeExistsAsync();

        var publisher = _serviceProvider.GetRequiredService<IRabbitMQPublisher>();
        var job = CreateTestScheduledJob("RoutingJob", "routing-worker");
        job.RoutingPattern = "custom.routing.*";
        var correlationId = Guid.CreateVersion7();

        // Bind queue for the expected routing key (custom.routing.job)
        var queueName = await BindTemporaryQueueAsync("custom.routing.job");

        // Act
        var result = await publisher.PublishJobAsync(job, correlationId);

        // Assert
        result.Should().BeTrue();

        await Task.Delay(500);
        var messageCount = await GetQueueMessageCountAsync(queueName);
        messageCount.Should().BeGreaterThan(0);
    }

    private static ScheduledJob CreateTestScheduledJob(string jobName, string workerId) => new()
    {
        Id = Guid.CreateVersion7(),
        DisplayName = $"Test {jobName}",
        Description = $"Test job for {jobName}",
        JobNameInWorker = jobName,
        JobData = "{}",
        ExecuteAt = DateTime.UtcNow,
        IsActive = true,
        WorkerId = workerId,
        RoutingPattern = null,
        ConcurrentExecutionPolicy = ConcurrentExecutionPolicy.Skip,
        CreationDate = DateTime.UtcNow,
        CreatorUserName = "TestUser"
    };

    private async Task EnsureExchangeExistsAsync()
    {
        var connectionFactory = _serviceProvider.GetRequiredService<RabbitMQConnectionFactory>();
        await using var channel = await connectionFactory.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: WorkerConstant.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
    }

    private async Task<string> BindTemporaryQueueAsync(string routingKey)
    {
        var rabbitFactory = new ConnectionFactory
        {
            HostName = _factory.GetRabbitMqHost(),
            Port = _factory.GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        var connection = await rabbitFactory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        var queueDeclare = await channel.QueueDeclareAsync(
            queue: $"test-{routingKey}-{Guid.CreateVersion7():N}",
            durable: false,
            exclusive: false,
            autoDelete: true);

        await channel.QueueBindAsync(
            queue: queueDeclare.QueueName,
            exchange: WorkerConstant.ExchangeName,
            routingKey: routingKey);

        return queueDeclare.QueueName;
    }

    private async Task<uint> GetQueueMessageCountAsync(string queueName)
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

        var queueInfo = await channel.QueueDeclarePassiveAsync(queueName);
        return queueInfo.MessageCount;
    }
}
