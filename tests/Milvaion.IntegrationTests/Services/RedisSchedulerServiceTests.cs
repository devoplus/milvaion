using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisSchedulerService.
/// Tests ZSET-based job scheduling operations against real Redis.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class RedisSchedulerServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task AddToScheduledSetAsync_ShouldAddJobToZSet()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();
        var executeAt = DateTime.UtcNow.AddMinutes(10);

        // Act
        var added = await schedulerService.AddToScheduledSetAsync(jobId, executeAt);

        // Assert
        added.Should().BeTrue();

        var scheduledTime = await schedulerService.GetScheduledTimeAsync(jobId);
        scheduledTime.Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveFromScheduledSetAsync_ShouldRemoveJobFromZSet()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();

        await schedulerService.AddToScheduledSetAsync(jobId, DateTime.UtcNow.AddMinutes(10));

        // Act
        var removed = await schedulerService.RemoveFromScheduledSetAsync(jobId);

        // Assert
        removed.Should().BeTrue();

        var scheduledTime = await schedulerService.GetScheduledTimeAsync(jobId);
        scheduledTime.Should().BeNull();
    }

    [Fact]
    public async Task GetDueJobsAsync_ShouldReturnOnlyDueJobs()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var dueJobId = Guid.CreateVersion7();
        var futureJobId = Guid.CreateVersion7();

        await schedulerService.AddToScheduledSetAsync(dueJobId, DateTime.UtcNow.AddMinutes(-5));
        await schedulerService.AddToScheduledSetAsync(futureJobId, DateTime.UtcNow.AddMinutes(30));

        // Act
        var dueJobs = await schedulerService.GetDueJobsAsync(DateTime.UtcNow);

        // Assert
        dueJobs.Should().Contain(dueJobId);
        dueJobs.Should().NotContain(futureJobId);
    }

    [Fact]
    public async Task GetDueJobsAsync_ShouldReturnEmptyList_WhenNoDueJobs()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        await schedulerService.AddToScheduledSetAsync(Guid.CreateVersion7(), DateTime.UtcNow.AddMinutes(30));

        // Act
        var dueJobs = await schedulerService.GetDueJobsAsync(DateTime.UtcNow);

        // Assert
        dueJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateScheduleAsync_ShouldUpdateJobExecuteAt()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();
        var originalTime = DateTime.UtcNow.AddMinutes(10);
        var newTime = DateTime.UtcNow.AddMinutes(30);

        await schedulerService.AddToScheduledSetAsync(jobId, originalTime);

        // Act
        var updated = await schedulerService.UpdateScheduleAsync(jobId, newTime);

        // Assert
        updated.Should().BeTrue();

        var scheduledTime = await schedulerService.GetScheduledTimeAsync(jobId);
        scheduledTime.Should().NotBeNull();
        scheduledTime.Value.Should().BeCloseTo(newTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetScheduledTimeAsync_ShouldReturnNull_WhenJobNotFound()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();

        // Act
        var scheduledTime = await schedulerService.GetScheduledTimeAsync(jobId);

        // Assert
        scheduledTime.Should().BeNull();
    }

    [Fact]
    public async Task GetScheduledJobsCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        await schedulerService.AddToScheduledSetAsync(Guid.CreateVersion7(), DateTime.UtcNow.AddMinutes(10));
        await schedulerService.AddToScheduledSetAsync(Guid.CreateVersion7(), DateTime.UtcNow.AddMinutes(20));
        await schedulerService.AddToScheduledSetAsync(Guid.CreateVersion7(), DateTime.UtcNow.AddMinutes(30));

        // Act
        var count = await schedulerService.GetScheduledJobsCountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task RemoveFromScheduledSetBulkAsync_ShouldRemoveMultipleJobs()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobIds = new List<Guid>();

        for (int i = 0; i < 5; i++)
        {
            var jobId = Guid.CreateVersion7();
            jobIds.Add(jobId);
            await schedulerService.AddToScheduledSetAsync(jobId, DateTime.UtcNow.AddMinutes(i + 1));
        }

        // Act
        var removed = await schedulerService.RemoveFromScheduledSetBulkAsync(jobIds.Take(3));

        // Assert
        removed.Should().Be(3);

        var count = await schedulerService.GetScheduledJobsCountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetDueJobsAsync_ShouldRespectLimit()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        for (int i = 0; i < 10; i++)
            await schedulerService.AddToScheduledSetAsync(Guid.CreateVersion7(), DateTime.UtcNow.AddMinutes(-i - 1));

        // Act
        var dueJobs = await schedulerService.GetDueJobsAsync(DateTime.UtcNow, limit: 5);

        // Assert
        dueJobs.Should().HaveCount(5);
    }

    #region RemoveAllRunningJobsForWorkerAsync

    [Fact]
    public async Task RemoveAllRunningJobsForWorkerAsync_ShouldRemoveJobs_WhenWorkerSpecificSetExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var db = GetRedisDatabase();
        var keyPrefix = GetKeyPrefix();

        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();

        // Add jobs to global running set
        await db.SetAddAsync($"{keyPrefix}running_jobs", jobId1.ToString());
        await db.SetAddAsync($"{keyPrefix}running_jobs", jobId2.ToString());

        // Add jobs to worker-specific running set
        await db.SetAddAsync($"{keyPrefix}running_jobs_by_worker:worker-01", jobId1.ToString());
        await db.SetAddAsync($"{keyPrefix}running_jobs_by_worker:worker-01", jobId2.ToString());

        // Act
        var removed = await schedulerService.RemoveAllRunningJobsForWorkerAsync("worker-01");

        // Assert
        removed.Should().Be(2);

        var isRunning1 = await schedulerService.IsJobRunningAsync(jobId1);
        var isRunning2 = await schedulerService.IsJobRunningAsync(jobId2);
        isRunning1.Should().BeFalse();
        isRunning2.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAllRunningJobsForWorkerAsync_ShouldReturnZero_WhenWorkerIdIsEmpty()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var removed = await schedulerService.RemoveAllRunningJobsForWorkerAsync("");

        // Assert
        removed.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAllRunningJobsForWorkerAsync_ShouldReturnZero_WhenNoRunningJobsForWorker()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var removed = await schedulerService.RemoveAllRunningJobsForWorkerAsync("nonexistent-worker");

        // Assert
        removed.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAllRunningJobsForWorkerAsync_ShouldUseFallback_WhenNoWorkerSpecificSet()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var db = GetRedisDatabase();
        var keyPrefix = GetKeyPrefix();

        var jobId = Guid.CreateVersion7();

        // Add job to global running set only (no worker-specific set)
        await db.SetAddAsync($"{keyPrefix}running_jobs", jobId.ToString());

        // Cache the job details with WorkerId so fallback scan can find it
        await schedulerService.CacheJobDetailsAsync(CreateTestJob(jobId, workerId: "fallback-worker"));

        // Act
        var removed = await schedulerService.RemoveAllRunningJobsForWorkerAsync("fallback-worker");

        // Assert
        removed.Should().Be(1);

        var isRunning = await schedulerService.IsJobRunningAsync(jobId);
        isRunning.Should().BeFalse();
    }

    #endregion

    #region GetCachedJobAsync

    [Fact]
    public async Task GetCachedJobAsync_ShouldReturnCachedJob_WhenJobExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();
        var job = CreateTestJob(jobId, displayName: "Cached Test Job", workerId: "cache-worker");

        await schedulerService.CacheJobDetailsAsync(job);

        // Act
        var cachedJob = await schedulerService.GetCachedJobAsync(jobId);

        // Assert
        cachedJob.Should().NotBeNull();
        cachedJob!.Id.Should().Be(jobId);
        cachedJob.DisplayName.Should().Be("Cached Test Job");
        cachedJob.WorkerId.Should().Be("cache-worker");
        cachedJob.JobNameInWorker.Should().Be("TestJob");
        cachedJob.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCachedJobAsync_ShouldReturnNull_WhenJobNotInCache()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var cachedJob = await schedulerService.GetCachedJobAsync(Guid.CreateVersion7());

        // Assert
        cachedJob.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedJobAsync_ShouldReturnExternalJobFields()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();
        var job = CreateTestJob(jobId, isExternal: true, externalJobId: "DEFAULT.QuartzJob_001");

        await schedulerService.CacheJobDetailsAsync(job);

        // Act
        var cachedJob = await schedulerService.GetCachedJobAsync(jobId);

        // Assert
        cachedJob.Should().NotBeNull();
        cachedJob!.IsExternal.Should().BeTrue();
        cachedJob.ExternalJobId.Should().Be("DEFAULT.QuartzJob_001");
    }

    #endregion

    #region RemoveCachedJobsBulkAsync

    [Fact]
    public async Task RemoveCachedJobsBulkAsync_ShouldRemoveMultipleJobs()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();
        var jobId3 = Guid.CreateVersion7();

        await schedulerService.CacheJobDetailsAsync(CreateTestJob(jobId1));
        await schedulerService.CacheJobDetailsAsync(CreateTestJob(jobId2));
        await schedulerService.CacheJobDetailsAsync(CreateTestJob(jobId3));

        // Act
        var removed = await schedulerService.RemoveCachedJobsBulkAsync([jobId1, jobId2]);

        // Assert
        removed.Should().Be(2);

        var cached1 = await schedulerService.GetCachedJobAsync(jobId1);
        var cached2 = await schedulerService.GetCachedJobAsync(jobId2);
        var cached3 = await schedulerService.GetCachedJobAsync(jobId3);

        cached1.Should().BeNull();
        cached2.Should().BeNull();
        cached3.Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveCachedJobsBulkAsync_ShouldReturnZero_WhenListIsEmpty()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var removed = await schedulerService.RemoveCachedJobsBulkAsync([]);

        // Assert
        removed.Should().Be(0);
    }

    [Fact]
    public async Task RemoveCachedJobsBulkAsync_ShouldReturnZero_WhenJobsNotInCache()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var removed = await schedulerService.RemoveCachedJobsBulkAsync([Guid.CreateVersion7(), Guid.CreateVersion7()]);

        // Assert
        removed.Should().Be(0);
    }

    #endregion

    #region GetJobIdByExternalIdAsync

    [Fact]
    public async Task GetJobIdByExternalIdAsync_ShouldReturnJobId_WhenMappingExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();

        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.TestJob", jobId);

        // Act
        var result = await schedulerService.GetJobIdByExternalIdAsync("DEFAULT.TestJob");

        // Assert
        result.Should().Be(jobId);
    }

    [Fact]
    public async Task GetJobIdByExternalIdAsync_ShouldReturnNull_WhenMappingDoesNotExist()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var result = await schedulerService.GetJobIdByExternalIdAsync("DEFAULT.NonExistentJob");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobIdByExternalIdAsync_ShouldReturnNull_WhenExternalIdIsEmpty()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var result = await schedulerService.GetJobIdByExternalIdAsync("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobIdByExternalIdAsync_ShouldReturnJobId_WhenSetViaCacheJobDetails()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();
        var externalJobId = "DEFAULT.CachedExternalJob";

        // CacheJobDetailsAsync also creates external mapping for external jobs
        await schedulerService.CacheJobDetailsAsync(CreateTestJob(jobId, isExternal: true, externalJobId: externalJobId));

        // Act
        var result = await schedulerService.GetJobIdByExternalIdAsync(externalJobId);

        // Assert
        result.Should().Be(jobId);
    }

    #endregion

    #region GetJobIdsByExternalIdsBulkAsync

    [Fact]
    public async Task GetJobIdsByExternalIdsBulkAsync_ShouldReturnAllMappings()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();

        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.Job1", jobId1);
        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.Job2", jobId2);

        // Act
        var result = await schedulerService.GetJobIdsByExternalIdsBulkAsync(["DEFAULT.Job1", "DEFAULT.Job2"]);

        // Assert
        result.Should().HaveCount(2);
        result["DEFAULT.Job1"].Should().Be(jobId1);
        result["DEFAULT.Job2"].Should().Be(jobId2);
    }

    [Fact]
    public async Task GetJobIdsByExternalIdsBulkAsync_ShouldReturnOnlyExistingMappings()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId1 = Guid.CreateVersion7();

        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.ExistingJob", jobId1);

        // Act
        var result = await schedulerService.GetJobIdsByExternalIdsBulkAsync(["DEFAULT.ExistingJob", "DEFAULT.MissingJob"]);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("DEFAULT.ExistingJob");
        result.Should().NotContainKey("DEFAULT.MissingJob");
    }

    [Fact]
    public async Task GetJobIdsByExternalIdsBulkAsync_ShouldReturnEmpty_WhenListIsEmpty()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act
        var result = await schedulerService.GetJobIdsByExternalIdsBulkAsync([]);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region SetExternalJobIdMappingAsync

    [Fact]
    public async Task SetExternalJobIdMappingAsync_ShouldCreateMapping()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId = Guid.CreateVersion7();

        // Act
        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.NewMapping", jobId);

        // Assert
        var result = await schedulerService.GetJobIdByExternalIdAsync("DEFAULT.NewMapping");
        result.Should().Be(jobId);
    }

    [Fact]
    public async Task SetExternalJobIdMappingAsync_ShouldOverwriteExistingMapping()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var oldJobId = Guid.CreateVersion7();
        var newJobId = Guid.CreateVersion7();

        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.OverwriteJob", oldJobId);

        // Act
        await schedulerService.SetExternalJobIdMappingAsync("DEFAULT.OverwriteJob", newJobId);

        // Assert
        var result = await schedulerService.GetJobIdByExternalIdAsync("DEFAULT.OverwriteJob");
        result.Should().Be(newJobId);
    }

    #endregion

    #region SetExternalJobIdMappingsBulkAsync

    [Fact]
    public async Task SetExternalJobIdMappingsBulkAsync_ShouldCreateMultipleMappings()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();
        var jobId3 = Guid.CreateVersion7();

        var mappings = new Dictionary<string, Guid>
        {
            ["DEFAULT.BulkJob1"] = jobId1,
            ["DEFAULT.BulkJob2"] = jobId2,
            ["DEFAULT.BulkJob3"] = jobId3
        };

        // Act
        await schedulerService.SetExternalJobIdMappingsBulkAsync(mappings);

        // Assert
        var result = await schedulerService.GetJobIdsByExternalIdsBulkAsync(["DEFAULT.BulkJob1", "DEFAULT.BulkJob2", "DEFAULT.BulkJob3"]);
        result.Should().HaveCount(3);
        result["DEFAULT.BulkJob1"].Should().Be(jobId1);
        result["DEFAULT.BulkJob2"].Should().Be(jobId2);
        result["DEFAULT.BulkJob3"].Should().Be(jobId3);
    }

    [Fact]
    public async Task SetExternalJobIdMappingsBulkAsync_ShouldHandleEmptyDictionary()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();

        // Act & Assert - should not throw
        await schedulerService.SetExternalJobIdMappingsBulkAsync([]);
    }

    [Fact]
    public async Task SetExternalJobIdMappingsBulkAsync_ShouldSkipNullOrEmptyKeys()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var schedulerService = GetRedisSchedulerService();
        var jobId1 = Guid.CreateVersion7();
        var jobId2 = Guid.CreateVersion7();

        var mappings = new Dictionary<string, Guid>
        {
            ["DEFAULT.ValidJob"] = jobId1,
            [""] = jobId2
        };

        // Act
        await schedulerService.SetExternalJobIdMappingsBulkAsync(mappings);

        // Assert
        var result = await schedulerService.GetJobIdByExternalIdAsync("DEFAULT.ValidJob");
        result.Should().Be(jobId1);
    }

    #endregion

    #region Helpers

    private string GetKeyPrefix()
    {
        var options = _serviceProvider.GetRequiredService<IOptions<RedisOptions>>();
        return options.Value.KeyPrefix;
    }

    private static ScheduledJob CreateTestJob(
        Guid? jobId = null,
        string displayName = "Test Job",
        string workerId = null,
        string cronExpression = null,
        bool isExternal = false,
        string externalJobId = null) => new()
        {
            Id = jobId ?? Guid.CreateVersion7(),
            DisplayName = displayName,
            Description = "Test job description",
            JobNameInWorker = "TestJob",
            JobData = "{}",
            ExecuteAt = DateTime.UtcNow.AddMinutes(10),
            CronExpression = cronExpression,
            IsActive = true,
            ConcurrentExecutionPolicy = ConcurrentExecutionPolicy.Skip,
            WorkerId = workerId,
            RoutingPattern = workerId != null ? $"worker.{workerId}" : null,
            Version = 1,
            CreationDate = DateTime.UtcNow,
            CreatorUserName = "TestUser",
            IsExternal = isExternal,
            ExternalJobId = externalJobId
        };

    #endregion
}
