using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.IntegrationTests.TestBase;
using StackExchange.Redis;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for JobCancellationService.
/// Tests Redis Pub/Sub based job cancellation message publishing against real Redis.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class JobCancellationServiceTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task PublishCancellationAsync_ShouldPublishMessage()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = _serviceProvider.GetRequiredService<IJobCancellationService>();
        var correlationId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var occurrenceId = Guid.CreateVersion7();
        var reason = "Test cancellation reason";

        // Act
        var subscriberCount = await cancellationService.PublishCancellationAsync(correlationId, jobId, occurrenceId, reason);

        // Assert - No subscribers connected, publish should succeed
        subscriberCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PublishCancellationAsync_ShouldDeliverMessageToSubscriber()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = _serviceProvider.GetRequiredService<IJobCancellationService>();
        var redis = GetRedisConnection();
        var options = _serviceProvider.GetRequiredService<IOptions<RedisOptions>>();

        var correlationId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var occurrenceId = Guid.CreateVersion7();
        var reason = "Integration test cancellation";

        string receivedMessage = null;
        var tcs = new TaskCompletionSource<bool>();

        // Subscribe to the cancellation channel
        var subscriber = redis.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(options.Value.CancellationChannel), (channel, message) =>
        {
            receivedMessage = message.ToString();
            tcs.TrySetResult(true);
        });

        await Task.Delay(500);

        // Act
        await cancellationService.PublishCancellationAsync(correlationId, jobId, occurrenceId, reason);

        // Wait for message
        var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == tcs.Task;

        // Assert
        received.Should().BeTrue("cancellation message should be received");
        receivedMessage.Should().NotBeNullOrEmpty();

        using var doc = JsonDocument.Parse(receivedMessage);
        doc.RootElement.GetProperty("CorrelationId").GetString().Should().Be(correlationId.ToString());
        doc.RootElement.GetProperty("JobId").GetString().Should().Be(jobId.ToString());
        doc.RootElement.GetProperty("OccurrenceId").GetString().Should().Be(occurrenceId.ToString());
        doc.RootElement.GetProperty("Reason").GetString().Should().Be(reason);
    }

    [Fact]
    public async Task PublishCancellationAsync_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = _serviceProvider.GetRequiredService<IJobCancellationService>();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            var result = await cancellationService.PublishCancellationAsync(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                $"Cancellation reason {i}");

            result.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}
