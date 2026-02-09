using FluentAssertions;
using Milvaion.IntegrationTests.TestBase;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisSchedulerService.
/// Tests ZSET-based job scheduling operations against real Redis.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class RedisSchedulerServiceTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
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
}
