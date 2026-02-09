using FluentAssertions;
using Milvaion.IntegrationTests.TestBase;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisCancellationService.
/// Tests Redis Pub/Sub based job cancellation against real Redis.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class RedisCancellationServiceTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task PublishCancellationAsync_ShouldPublishWithoutError()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = GetRedisCancellationService();
        var jobId = Guid.CreateVersion7();

        // Act
        var subscriberCount = await cancellationService.PublishCancellationAsync(jobId);

        // Assert - No subscribers connected, but publish should succeed
        subscriberCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SubscribeToCancellationsAsync_ShouldReceiveCancellationSignal()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = GetRedisCancellationService();
        var jobId = Guid.CreateVersion7();
        Guid? receivedJobId = null;
        var tcs = new TaskCompletionSource<bool>();

        // Act - Subscribe first
        await cancellationService.SubscribeToCancellationsAsync(id =>
        {
            receivedJobId = id;
            tcs.TrySetResult(true);
        });

        // Give subscription time to establish
        await Task.Delay(500);

        // Publish cancellation
        await cancellationService.PublishCancellationAsync(jobId);

        // Wait for reception with timeout
        var received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))) == tcs.Task;

        // Assert
        received.Should().BeTrue("cancellation signal should be received");
        receivedJobId.Should().Be(jobId);
    }

    [Fact]
    public async Task PublishCancellationAsync_MultipleTimes_ShouldSucceed()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var cancellationService = GetRedisCancellationService();

        // Act & Assert - Should not throw
        for (int i = 0; i < 5; i++)
        {
            var subscriberCount = await cancellationService.PublishCancellationAsync(Guid.CreateVersion7());
            subscriberCount.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}
