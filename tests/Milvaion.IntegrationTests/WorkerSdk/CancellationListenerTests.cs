using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Services;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for CancellationListener.
/// Tests Redis Pub/Sub based cancellation signaling against real Redis.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class CancellationListenerTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    private static readonly string _cancellationChannel = "milvaion:test:job:cancel";

    [Fact]
    public async Task RegisterAndCancel_ShouldCancelJob_WhenSignalReceived()
    {
        // Arrange
        var options = CreateWorkerOptions();
        using var listener = new CancellationListener(Options.Create(options), GetLoggerFactory());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await listener.StartAsync(cts.Token);
        await Task.Delay(1000); // Wait for subscription

        var jobId = Guid.CreateVersion7();
        var jobCts = new CancellationTokenSource();
        listener.RegisterCancellation(jobId, jobCts);

        // Act - Publish cancellation signal via Redis
        var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionString());
        var subscriber = redis.GetSubscriber();
        await subscriber.PublishAsync(new RedisChannel(_cancellationChannel, RedisChannel.PatternMode.Literal), jobId.ToString());
        await Task.Delay(500);

        // Assert
        jobCts.IsCancellationRequested.Should().BeTrue("cancellation signal should have been received");

        cts.Cancel();
        redis.Dispose();
    }

    [Fact]
    public async Task RegisterAndCancel_ShouldNotAffectOtherJobs()
    {
        // Arrange
        var options = CreateWorkerOptions();
        using var listener = new CancellationListener(Options.Create(options), GetLoggerFactory());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await listener.StartAsync(cts.Token);
        await Task.Delay(1000);

        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();
        var jobCts1 = new CancellationTokenSource();
        var jobCts2 = new CancellationTokenSource();

        listener.RegisterCancellation(jobId1, jobCts1);
        listener.RegisterCancellation(jobId2, jobCts2);

        // Act - Cancel only job 1
        var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionString());
        var subscriber = redis.GetSubscriber();
        await subscriber.PublishAsync(new RedisChannel(_cancellationChannel, RedisChannel.PatternMode.Literal), jobId1.ToString());
        await Task.Delay(500);

        // Assert
        jobCts1.IsCancellationRequested.Should().BeTrue();
        jobCts2.IsCancellationRequested.Should().BeFalse("only job1 should be cancelled");

        cts.Cancel();
        redis.Dispose();
    }

    [Fact]
    public async Task UnregisterCancellation_ShouldPreventCancellation()
    {
        // Arrange
        var options = CreateWorkerOptions();
        using var listener = new CancellationListener(Options.Create(options), GetLoggerFactory());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await listener.StartAsync(cts.Token);
        await Task.Delay(1000);

        var jobId = Guid.CreateVersion7();
        var jobCts = new CancellationTokenSource();
        listener.RegisterCancellation(jobId, jobCts);

        // Unregister before signal
        listener.UnregisterCancellation(jobId);

        // Act - Publish cancellation signal
        var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionString());
        var subscriber = redis.GetSubscriber();
        await subscriber.PublishAsync(new RedisChannel(_cancellationChannel, RedisChannel.PatternMode.Literal), jobId.ToString());
        await Task.Delay(500);

        // Assert
        jobCts.IsCancellationRequested.Should().BeFalse("unregistered job should not be cancelled");

        cts.Cancel();
        redis.Dispose();
    }

    [Fact]
    public async Task Listener_ShouldIgnoreInvalidMessages()
    {
        // Arrange
        var options = CreateWorkerOptions();
        using var listener = new CancellationListener(Options.Create(options), GetLoggerFactory());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await listener.StartAsync(cts.Token);
        await Task.Delay(1000);

        // Act - Publish invalid message
        var redis = await ConnectionMultiplexer.ConnectAsync(GetRedisConnectionString());
        var subscriber = redis.GetSubscriber();

        var act = async () =>
        {
            await subscriber.PublishAsync(new RedisChannel(_cancellationChannel, RedisChannel.PatternMode.Literal), "not-a-guid");
            await Task.Delay(300);
        };

        // Assert - Should not throw
        await act.Should().NotThrowAsync();

        cts.Cancel();
        redis.Dispose();
    }

    private WorkerOptions CreateWorkerOptions() => new()
    {
        WorkerId = "test-worker",
        Redis = new RedisSettings
        {
            ConnectionString = GetRedisConnectionString(),
            CancellationChannel = _cancellationChannel
        }
    };
}
