using FluentAssertions;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisStatsService.
/// Tests atomic counter operations and statistics retrieval against real Redis.
/// </summary>
[Collection(nameof(MilvaionTestCollection))]
public class RedisStatsServiceTests(CustomWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task IncrementTotalOccurrencesAsync_ShouldIncrementCounter()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act
        await statsService.IncrementTotalOccurrencesAsync();
        await statsService.IncrementTotalOccurrencesAsync();
        await statsService.IncrementTotalOccurrencesAsync();

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Total"].Should().Be(3);
    }

    [Fact]
    public async Task IncrementTotalOccurrencesAsync_WithCount_ShouldIncrementByAmount()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act
        await statsService.IncrementTotalOccurrencesAsync(10);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Total"].Should().Be(10);
    }

    [Fact]
    public async Task IncrementStatusCounterAsync_ShouldIncrementCorrectStatusCounter()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Queued);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Running);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Completed);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Failed);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Failed);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Queued"].Should().Be(1);
        stats["Running"].Should().Be(1);
        stats["Completed"].Should().Be(1);
        stats["Failed"].Should().Be(2);
    }

    [Fact]
    public async Task DecrementStatusCounterAsync_ShouldDecrementCounter()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Running);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Running);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Running);

        // Act
        await statsService.DecrementStatusCounterAsync(JobOccurrenceStatus.Running);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Running"].Should().Be(2);
    }

    [Fact]
    public async Task DecrementStatusCounterAsync_ShouldNotGoNegative()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act - Decrement without prior increment
        await statsService.DecrementStatusCounterAsync(JobOccurrenceStatus.Running);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Running"].Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task UpdateStatusCountersAsync_ShouldAtomicallyUpdateBothCounters()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Queued);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Queued);

        // Act - Transition from Queued to Running
        await statsService.UpdateStatusCountersAsync(JobOccurrenceStatus.Queued, JobOccurrenceStatus.Running);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Queued"].Should().Be(1);
        stats["Running"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnAllCounters()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act
        var stats = await statsService.GetStatisticsAsync();

        // Assert
        stats.Should().ContainKey("Total");
        stats.Should().ContainKey("Queued");
        stats.Should().ContainKey("Running");
        stats.Should().ContainKey("Completed");
        stats.Should().ContainKey("Failed");
        stats.Should().ContainKey("Cancelled");
        stats.Should().ContainKey("TimedOut");
        stats.Should().ContainKey("Unknown");
        stats.Should().ContainKey("DurationSum");
        stats.Should().ContainKey("DurationCount");
    }

    [Fact]
    public async Task ResetCountersAsync_ShouldClearAllCounters()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        await statsService.IncrementTotalOccurrencesAsync(100);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Completed, 50);
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Failed, 10);

        // Act
        await statsService.ResetCountersAsync();

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Total"].Should().Be(0);
        stats["Completed"].Should().Be(0);
        stats["Failed"].Should().Be(0);
    }

    [Fact]
    public async Task IncrementStatusCounterAsync_WithCount_ShouldIncrementByAmount()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var statsService = GetRedisStatsService();

        // Act
        await statsService.IncrementStatusCounterAsync(JobOccurrenceStatus.Completed, 25);

        // Assert
        var stats = await statsService.GetStatisticsAsync();
        stats["Completed"].Should().Be(25);
    }
}
