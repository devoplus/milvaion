using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.BackgroundServices;
using Milvaion.Infrastructure.Telemetry;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.BackgroundServices;

/// <summary>
/// Integration tests for ZombieOccurrenceDetectorService.
/// Tests zombie detection for stuck Queued occurrences and job-specific timeouts.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class ZombieOccurrenceDetectorServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : BackgroundServiceTestBase(factory, output)
{
    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldMarkStuckOccurrencesAsUnkown()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"ZombieTestJob_{Guid.CreateVersion7():N}");

        // Create an occurrence that is "stuck" in Queued status for longer than the timeout
        var stuckOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-15) // Older than default 10 minute timeout
        );

        // Create a recent occurrence that should NOT be marked as zombie
        var recentOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-1) // Very recent, should not be zombie
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act - Start the service
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for zombie detection
        var found = await WaitForConditionAsync(
            async () =>
            {
                var stuck = await GetOccurrenceAsync(stuckOccurrence.Id);
                return stuck?.Status == JobOccurrenceStatus.Unknown;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("stuck occurrence should be marked as Failed");

        var stuckResult = await GetOccurrenceAsync(stuckOccurrence.Id);
        var recentResult = await GetOccurrenceAsync(recentOccurrence.Id);

        stuckResult.Status.Should().Be(JobOccurrenceStatus.Unknown);
        stuckResult.Exception.Should().Contain("Zombie occurrence detected");
        stuckResult.EndTime.Should().NotBeNull();

        // Recent occurrence should still be Queued
        recentResult.Status.Should().Be(JobOccurrenceStatus.Queued);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldRespectJobSpecificTimeout()
    {
        // Arrange
        await InitializeAsync();

        // Create a job with a longer timeout
        var longTimeoutJob = await SeedScheduledJobAsync(
            $"LongRunningJob_{Guid.CreateVersion7():N}",
            timeoutMinutes: 60 // 60 minute timeout
        );

        // Create occurrence that would be zombie with default timeout but NOT with job-specific timeout
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: longTimeoutJob.Id,
            jobName: longTimeoutJob.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-15), // 15 minutes old
            timeoutMinutes: 60 // Job-specific 60 minute timeout
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait a reasonable time for detection cycle
        await Task.Delay(2000, cts.Token);

        await service.StopAsync(cts.Token);

        // Assert - Should NOT be marked as zombie due to job-specific timeout
        var result = await GetOccurrenceAsync(occurrence.Id);
        result.Status.Should().Be(JobOccurrenceStatus.Queued);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldUpdateStatusChangeLogs()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"StatusLogTestJob_{Guid.CreateVersion7():N}");

        var zombieOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-20)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for zombie detection
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(zombieOccurrence.Id);
                return occ?.StatusChangeLogs?.Any(s => s.To == JobOccurrenceStatus.Unknown) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("status change log should be recorded");

        var result = await GetOccurrenceAsync(zombieOccurrence.Id);
        result.StatusChangeLogs.Should().NotBeNull();
        result.StatusChangeLogs.Should().NotBeEmpty();

        var statusChange = result.StatusChangeLogs.First();
        statusChange.From.Should().Be(JobOccurrenceStatus.Queued);
        statusChange.To.Should().Be(JobOccurrenceStatus.Unknown);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldAddLogEntry()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"LogTestJob_{Guid.CreateVersion7():N}");

        var zombieOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-15)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for zombie detection
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(zombieOccurrence.Id);
                return occ?.Logs?.Any(l => l.Category == "ZombieDetector") == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("zombie log entry should be added");

        var result = await GetOccurrenceAsync(zombieOccurrence.Id);
        result.Logs.Should().NotBeEmpty();

        var zombieLog = result.Logs.FirstOrDefault(l => l.Category == "ZombieDetector");
        zombieLog.Should().NotBeNull();
        zombieLog!.Level.Should().Be("Error");
        zombieLog.Message.Should().Contain("Zombie occurrence detected");
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldMarkAffectRunningOccurrences()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"RunningTestJob_{Guid.CreateVersion7():N}");

        // Create a Running occurrence that is old (but Running, not Queued)
        var runningOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running,
            createdAt: DateTime.UtcNow.AddMinutes(-30)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait enough time for detection cycles
        await Task.Delay(2000, cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        var result = await GetOccurrenceAsync(runningOccurrence.Id);
        result.Status.Should().Be(JobOccurrenceStatus.Unknown);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldCalculateDurationMs()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"DurationTestJob_{Guid.CreateVersion7():N}");
        var createdAt = DateTime.UtcNow.AddMinutes(-15);

        var zombieOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: createdAt
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for zombie detection
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(zombieOccurrence.Id);
                return occ?.DurationMs != null && occ.DurationMs > 0;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("duration should be calculated");

        var result = await GetOccurrenceAsync(zombieOccurrence.Id);
        result.DurationMs.Should().NotBeNull();
        result.DurationMs.Should().BeGreaterThan(0);

        // Duration should be approximately 15 minutes (900000 ms) with some tolerance
        var expectedDurationMs = (DateTime.UtcNow - createdAt).TotalMilliseconds;
        result.DurationMs!.Value.Should().BeInRange((long)(expectedDurationMs - 60000), (long)(expectedDurationMs + 60000));
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldNotAffectCompletedOccurrences()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"CompletedSafeJob_{Guid.CreateVersion7():N}");

        // Create a Completed occurrence that is old - should NOT be touched
        var completedOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Completed,
            createdAt: DateTime.UtcNow.AddMinutes(-60)
        );

        // Create a Failed occurrence that is old - should NOT be touched
        var failedOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Failed,
            createdAt: DateTime.UtcNow.AddMinutes(-60)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(2000, cts.Token);

        await service.StopAsync(cts.Token);

        // Assert - Completed and Failed statuses should remain unchanged
        var completedResult = await GetOccurrenceAsync(completedOccurrence.Id);
        var failedResult = await GetOccurrenceAsync(failedOccurrence.Id);

        completedResult.Status.Should().Be(JobOccurrenceStatus.Completed);
        failedResult.Status.Should().Be(JobOccurrenceStatus.Failed);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldHandleMultipleZombiesInSingleCycle()
    {
        // Arrange
        await InitializeAsync();

        var job1 = await SeedScheduledJobAsync($"MultiZombie1_{Guid.CreateVersion7():N}");
        var job2 = await SeedScheduledJobAsync($"MultiZombie2_{Guid.CreateVersion7():N}");
        var job3 = await SeedScheduledJobAsync($"MultiZombie3_{Guid.CreateVersion7():N}");

        var zombie1 = await SeedJobOccurrenceAsync(
            jobId: job1.Id,
            jobName: job1.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-20)
        );

        var zombie2 = await SeedJobOccurrenceAsync(
            jobId: job2.Id,
            jobName: job2.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-25)
        );

        var zombie3 = await SeedJobOccurrenceAsync(
            jobId: job3.Id,
            jobName: job3.JobNameInWorker,
            status: JobOccurrenceStatus.Running,
            createdAt: DateTime.UtcNow.AddMinutes(-30)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for all zombies to be detected
        var found = await WaitForConditionAsync(
            async () =>
            {
                var z1 = await GetOccurrenceAsync(zombie1.Id);
                var z2 = await GetOccurrenceAsync(zombie2.Id);
                var z3 = await GetOccurrenceAsync(zombie3.Id);
                return z1?.Status == JobOccurrenceStatus.Unknown
                    && z2?.Status == JobOccurrenceStatus.Unknown
                    && z3?.Status == JobOccurrenceStatus.Unknown;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all zombie occurrences should be detected in a single cycle");

        var result1 = await GetOccurrenceAsync(zombie1.Id);
        var result2 = await GetOccurrenceAsync(zombie2.Id);
        var result3 = await GetOccurrenceAsync(zombie3.Id);

        result1.Status.Should().Be(JobOccurrenceStatus.Unknown);
        result1.Exception.Should().Contain("Zombie occurrence detected");

        result2.Status.Should().Be(JobOccurrenceStatus.Unknown);
        result2.Exception.Should().Contain("Zombie occurrence detected");

        result3.Status.Should().Be(JobOccurrenceStatus.Unknown);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldHandleMixedHealthyAndZombieOccurrences()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"MixedJob_{Guid.CreateVersion7():N}");

        // Healthy (recent) occurrences
        var healthyQueued = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-1)
        );

        var healthyRunning = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running,
            createdAt: DateTime.UtcNow.AddMinutes(-1)
        );

        // Zombie (old) occurrences
        var zombieQueued = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: DateTime.UtcNow.AddMinutes(-20)
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Wait for zombie detection
        var found = await WaitForConditionAsync(
            async () =>
            {
                var zombie = await GetOccurrenceAsync(zombieQueued.Id);
                return zombie?.Status == JobOccurrenceStatus.Unknown;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert - Only zombie should be marked, healthy ones untouched
        found.Should().BeTrue("zombie occurrence should be detected");

        var healthyQueuedResult = await GetOccurrenceAsync(healthyQueued.Id);
        var healthyRunningResult = await GetOccurrenceAsync(healthyRunning.Id);
        var zombieResult = await GetOccurrenceAsync(zombieQueued.Id);

        healthyQueuedResult.Status.Should().Be(JobOccurrenceStatus.Queued);
        healthyRunningResult.Status.Should().Be(JobOccurrenceStatus.Running);
        zombieResult.Status.Should().Be(JobOccurrenceStatus.Unknown);
    }

    [Fact]
    public async Task DetectAndCleanupZombieOccurrences_ShouldIncludeStuckDurationInException()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync($"DurationInfoJob_{Guid.CreateVersion7():N}");
        var createdAt = DateTime.UtcNow.AddMinutes(-25);

        var zombieOccurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued,
            createdAt: createdAt
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var service = CreateZombieDetectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(zombieOccurrence.Id);
                return occ?.Status == JobOccurrenceStatus.Unknown;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await service.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("zombie should be detected");

        var result = await GetOccurrenceAsync(zombieOccurrence.Id);
        result.Exception.Should().Contain("Zombie occurrence detected");
        result.Exception.Should().Contain("stuck in Queued status");
        result.Exception.Should().Contain("timeout: 10 minutes");
    }

    private ZombieOccurrenceDetectorService CreateZombieDetectorService() => new(
            _serviceProvider,
            _serviceProvider.GetRequiredService<IRedisSchedulerService>(),
            _serviceProvider.GetRequiredService<IRedisWorkerService>(),
            _serviceProvider.GetRequiredService<IRedisStatsService>(),
            _serviceProvider.GetRequiredService<IAlertNotifier>(),
            Options.Create(new ZombieOccurrenceDetectorOptions
            {
                Enabled = true,
                CheckIntervalSeconds = 1, // Fast interval for testing
                ZombieTimeoutMinutes = 10 // Default 10 minute timeout
            }),
            _serviceProvider.GetRequiredService<ILoggerFactory>(),
            _serviceProvider.GetRequiredService<BackgroundServiceMetrics>()
        );
}
