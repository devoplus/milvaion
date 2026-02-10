using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Constants;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.BackgroundServices;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Milvaion.Infrastructure.Telemetry;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.BackgroundServices;

/// <summary>
/// Integration tests for StatusTrackerService.
/// Tests status update consumption and occurrence state management.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class StatusTrackerServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : BackgroundServiceTestBase(factory, output)
{

    [Fact]
    public async Task ProcessStatusUpdate_ShouldUpdateOccurrenceStatus()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("StatusTestJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueWorkerId = $"test-worker-{Guid.CreateVersion7():N}";

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        // Publish status update to Running
        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = uniqueWorkerId,
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Running && occ?.WorkerId == uniqueWorkerId;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence status should be updated to Running");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Running);
        updatedOccurrence.WorkerId.Should().Be(uniqueWorkerId);
        updatedOccurrence.StartTime.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldTrackStatusChangeHistory()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("StatusHistoryJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        // Publish status update
        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.StatusChangeLogs?.Any(s => s.To == JobOccurrenceStatus.Running) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("status change log should be recorded");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.StatusChangeLogs.Should().NotBeNull();
        updatedOccurrence.StatusChangeLogs.Should().NotBeEmpty();

        var statusChange = updatedOccurrence.StatusChangeLogs.First();
        statusChange.From.Should().Be(JobOccurrenceStatus.Queued);
        statusChange.To.Should().Be(JobOccurrenceStatus.Running);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleCompletedStatus()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("CompletedJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var startTime = DateTime.UtcNow.AddSeconds(-10);
        var endTime = DateTime.UtcNow;
        var uniqueResult = $"Job completed successfully - {Guid.CreateVersion7():N}";

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Completed,
            StartTime = startTime,
            EndTime = endTime,
            DurationMs = 10000,
            Result = uniqueResult
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Completed && occ?.Result == uniqueResult;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should be marked as Completed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Completed);
        updatedOccurrence.EndTime.Should().NotBeNull();
        updatedOccurrence.DurationMs.Should().Be(10000);
        updatedOccurrence.Result.Should().Be(uniqueResult);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleFailedStatus()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("FailedJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueException = $"NullReferenceException: {Guid.CreateVersion7():N}";

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Failed,
            EndTime = DateTime.UtcNow,
            Exception = uniqueException
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Failed && occ?.Exception?.Contains(uniqueException) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should be marked as Failed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Failed);
        updatedOccurrence.Exception.Should().Contain(uniqueException);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldBatchProcessMultipleUpdates()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("BatchStatusJob");
        var uniqueWorkerId = $"batch-worker-{Guid.CreateVersion7():N}";

        var occurrences = new List<JobOccurrence>();
        for (int i = 0; i < 5; i++)
        {
            var occ = await SeedJobOccurrenceAsync(
                jobId: job.Id,
                jobName: job.JobNameInWorker,
                status: JobOccurrenceStatus.Queued
            );
            occurrences.Add(occ);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        // Publish status updates for all occurrences
        foreach (var occ in occurrences)
        {
            await PublishStatusUpdateAsync(new JobStatusUpdateMessage
            {
                CorrelationId = occ.CorrelationId,
                JobId = job.Id,
                WorkerId = uniqueWorkerId,
                Status = JobOccurrenceStatus.Running,
                StartTime = DateTime.UtcNow
            }, cts.Token);
        }

        // Wait for all occurrences to be updated
        var found = await WaitForConditionAsync(async () =>
        {
            foreach (var occ in occurrences)
            {
                var updated = await GetOccurrenceAsync(occ.Id, cts.Token);
                if (updated?.Status != JobOccurrenceStatus.Running || updated?.WorkerId != uniqueWorkerId)
                    return false;
            }

            return true;
        },
        timeout: TimeSpan.FromSeconds(20),
        pollInterval: TimeSpan.FromMilliseconds(500),
        cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all occurrences should be updated");

        foreach (var occ in occurrences)
        {
            var updated = await GetOccurrenceAsync(occ.Id, cts.Token);
            updated.Status.Should().Be(JobOccurrenceStatus.Running);
            updated.WorkerId.Should().Be(uniqueWorkerId);
        }
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldUpdateLastHeartbeat()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("HeartbeatJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var beforeUpdate = DateTime.UtcNow;

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "heartbeat-worker",
            Status = JobOccurrenceStatus.Running
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.LastHeartbeat != null && occ.LastHeartbeat > beforeUpdate.AddSeconds(-5);
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("heartbeat should be updated");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.LastHeartbeat.Should().NotBeNull();
        updatedOccurrence.LastHeartbeat.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldClearExceptionOnSuccess()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("ClearExceptionJob");

        // First seed with a failed status
        var dbContext = GetDbContext();
        var correlationId = Guid.CreateVersion7();
        var occurrence = new JobOccurrence
        {
            Id = correlationId,
            CorrelationId = correlationId,
            JobId = job.Id,
            JobName = job.JobNameInWorker,
            JobVersion = 1,
            Status = JobOccurrenceStatus.Running,
            Exception = "Previous error that should be cleared",
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.JobOccurrences.AddAsync(occurrence);
        await dbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var uniqueResult = $"Success-{Guid.CreateVersion7():N}";

        // Act - Start the tracker first
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            Result = uniqueResult
        }, cts.Token);

        // Wait for condition
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Completed && occ?.Result == uniqueResult;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should be marked as Completed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Completed);
        updatedOccurrence.Exception.Should().BeNull();
        updatedOccurrence.Result.Should().Be(uniqueResult);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleCancelledStatus()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("CancelledJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueResult = $"Cancelled by user - {Guid.CreateVersion7():N}";

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Cancelled,
            EndTime = DateTime.UtcNow,
            Result = uniqueResult
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Cancelled;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should be marked as Cancelled");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Cancelled);
        updatedOccurrence.EndTime.Should().NotBeNull();
        updatedOccurrence.Result.Should().Be(uniqueResult);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleTimedOutStatus()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("TimedOutJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueException = $"Execution timeout exceeded - {Guid.CreateVersion7():N}";

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.TimedOut,
            EndTime = DateTime.UtcNow,
            Exception = uniqueException
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.TimedOut && occ?.Exception?.Contains(uniqueException) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should be marked as TimedOut");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.TimedOut);
        updatedOccurrence.Exception.Should().Contain(uniqueException);
        updatedOccurrence.EndTime.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldTrackFullLifecycle_QueuedToRunningToCompleted()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("LifecycleJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var uniqueWorkerId = $"lifecycle-worker-{Guid.CreateVersion7():N}";

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        // Step 1: Queued -> Running
        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = uniqueWorkerId,
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        }, cts.Token);

        var runningFound = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Running;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        runningFound.Should().BeTrue("should transition to Running");

        // Step 2: Running -> Completed
        var uniqueResult = $"Lifecycle complete - {Guid.CreateVersion7():N}";
        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = uniqueWorkerId,
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            DurationMs = 5000,
            Result = uniqueResult
        }, cts.Token);

        var completedFound = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Completed && occ?.Result == uniqueResult;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert full lifecycle
        completedFound.Should().BeTrue("should transition to Completed");

        var finalOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        finalOccurrence.Status.Should().Be(JobOccurrenceStatus.Completed);
        finalOccurrence.WorkerId.Should().Be(uniqueWorkerId);
        finalOccurrence.Result.Should().Be(uniqueResult);
        finalOccurrence.DurationMs.Should().Be(5000);

        // Verify status change log has both transitions
        finalOccurrence.StatusChangeLogs.Should().NotBeNull();
        finalOccurrence.StatusChangeLogs.Should().HaveCountGreaterOrEqualTo(2);
        finalOccurrence.StatusChangeLogs.Should().Contain(s => s.From == JobOccurrenceStatus.Queued && s.To == JobOccurrenceStatus.Running);
        finalOccurrence.StatusChangeLogs.Should().Contain(s => s.From == JobOccurrenceStatus.Running && s.To == JobOccurrenceStatus.Completed);
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleMultipleOccurrencesForDifferentJobs()
    {
        // Arrange
        await InitializeAsync();

        var job1 = await SeedScheduledJobAsync("MultiTrackJob1");
        var job2 = await SeedScheduledJobAsync("MultiTrackJob2");
        var job3 = await SeedScheduledJobAsync("MultiTrackJob3");

        var occurrence1 = await SeedJobOccurrenceAsync(jobId: job1.Id, jobName: job1.JobNameInWorker, status: JobOccurrenceStatus.Queued);
        var occurrence2 = await SeedJobOccurrenceAsync(jobId: job2.Id, jobName: job2.JobNameInWorker, status: JobOccurrenceStatus.Queued);
        var occurrence3 = await SeedJobOccurrenceAsync(jobId: job3.Id, jobName: job3.JobNameInWorker, status: JobOccurrenceStatus.Queued);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        // Send different status updates for each occurrence
        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence1.CorrelationId,
            JobId = job1.Id,
            WorkerId = "worker-1",
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        }, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence2.CorrelationId,
            JobId = job2.Id,
            WorkerId = "worker-2",
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            DurationMs = 1000,
            Result = "done"
        }, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence3.CorrelationId,
            JobId = job3.Id,
            WorkerId = "worker-3",
            Status = JobOccurrenceStatus.Failed,
            EndTime = DateTime.UtcNow,
            Exception = "Test failure"
        }, cts.Token);

        // Wait for all updates
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ1 = await GetOccurrenceAsync(occurrence1.Id, cts.Token);
                var occ2 = await GetOccurrenceAsync(occurrence2.Id, cts.Token);
                var occ3 = await GetOccurrenceAsync(occurrence3.Id, cts.Token);
                return occ1?.Status == JobOccurrenceStatus.Running
                    && occ2?.Status == JobOccurrenceStatus.Completed
                    && occ3?.Status == JobOccurrenceStatus.Failed;
            },
            timeout: TimeSpan.FromSeconds(20),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all three occurrences should be updated with different statuses");

        var finalOcc1 = await GetOccurrenceAsync(occurrence1.Id, cts.Token);
        var finalOcc2 = await GetOccurrenceAsync(occurrence2.Id, cts.Token);
        var finalOcc3 = await GetOccurrenceAsync(occurrence3.Id, cts.Token);

        finalOcc1.Status.Should().Be(JobOccurrenceStatus.Running);
        finalOcc1.WorkerId.Should().Be("worker-1");

        finalOcc2.Status.Should().Be(JobOccurrenceStatus.Completed);
        finalOcc2.WorkerId.Should().Be("worker-2");
        finalOcc2.DurationMs.Should().Be(1000);

        finalOcc3.Status.Should().Be(JobOccurrenceStatus.Failed);
        finalOcc3.WorkerId.Should().Be("worker-3");
        finalOcc3.Exception.Should().Contain("Test failure");
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldRecordCorrectTimestampsInStatusChangeLog()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("TimestampLogJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Queued
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var beforeUpdate = DateTime.UtcNow;

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.StatusChangeLogs?.Any(s => s.To == JobOccurrenceStatus.Running) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("status change log should be recorded with timestamp");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        var statusLog = updatedOccurrence.StatusChangeLogs.First(s => s.To == JobOccurrenceStatus.Running);
        statusLog.Timestamp.Should().BeAfter(beforeUpdate.AddSeconds(-5));
        statusLog.Timestamp.Should().BeBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task ProcessStatusUpdate_ShouldHandleLongResultString()
    {
        // Arrange
        await InitializeAsync();

        var job = await SeedScheduledJobAsync("LongResultJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniquePrefix = $"Result-{Guid.CreateVersion7():N}-";
        var longResult = uniquePrefix + new string('R', 2000);

        // Act
        var tracker = CreateStatusTrackerService();
        _ = Task.Run(async () =>
        {
            try
            {
                await tracker.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(1000, cts.Token);

        await PublishStatusUpdateAsync(new JobStatusUpdateMessage
        {
            CorrelationId = occurrence.CorrelationId,
            JobId = job.Id,
            WorkerId = "test-worker",
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            Result = longResult
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Status == JobOccurrenceStatus.Completed && occ?.Result?.StartsWith(uniquePrefix) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await tracker.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("occurrence should handle long result string");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        updatedOccurrence.Status.Should().Be(JobOccurrenceStatus.Completed);
        updatedOccurrence.Result.Should().StartWith(uniquePrefix);
    }

    private StatusTrackerService CreateStatusTrackerService() => new(
            _serviceProvider,
            _serviceProvider.GetRequiredService<IRedisSchedulerService>(),
            _serviceProvider.GetRequiredService<IRedisStatsService>(),
            _serviceProvider.GetRequiredService<IAlertNotifier>(),
            _serviceProvider.GetRequiredService<RabbitMQConnectionFactory>(),
            Options.Create(new StatusTrackerOptions
            {
                Enabled = true,
                BatchSize = 10,
                BatchIntervalMs = 300 // Faster for tests
            }),
            Options.Create(new JobAutoDisableOptions
            {
                Enabled = true,
                ConsecutiveFailureThreshold = 5,
                FailureWindowMinutes = 60
            }),
            _serviceProvider.GetRequiredService<ILoggerFactory>(),
            _serviceProvider.GetRequiredService<BackgroundServiceMetrics>()
        );

    private async Task PublishStatusUpdateAsync(JobStatusUpdateMessage message, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _factory.GetRabbitMqHost(),
            Port = _factory.GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Ensure queue exists
        await channel.QueueDeclareAsync(
            queue: WorkerConstant.Queues.StatusUpdates,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, ConstantJsonOptions.PropNameCaseInsensitive));

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: WorkerConstant.Queues.StatusUpdates,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
