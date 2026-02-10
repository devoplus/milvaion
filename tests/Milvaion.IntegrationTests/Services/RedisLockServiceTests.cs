using FluentAssertions;
using Milvaion.IntegrationTests.TestBase;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Integration tests for RedisLockService.
/// Tests distributed lock acquire, release, extend, and query operations against real Redis.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class RedisLockServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : RedisServiceTestBase(factory, output)
{
    [Fact]
    public async Task TryAcquireLockAsync_ShouldAcquireLock_WhenNotAlreadyLocked()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();
        var workerId = "test-worker-01";

        // Act
        var acquired = await lockService.TryAcquireLockAsync(jobId, workerId, TimeSpan.FromSeconds(30));

        // Assert
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ShouldFail_WhenAlreadyLockedByAnotherWorker()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(30));

        // Act
        var acquired = await lockService.TryAcquireLockAsync(jobId, "worker-02", TimeSpan.FromSeconds(30));

        // Assert
        acquired.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_ShouldReleaseLock_WhenCalledByOwner()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();
        var workerId = "test-worker-01";

        await lockService.TryAcquireLockAsync(jobId, workerId, TimeSpan.FromSeconds(30));

        // Act
        var released = await lockService.ReleaseLockAsync(jobId, workerId);

        // Assert
        released.Should().BeTrue();

        var isLocked = await lockService.IsLockedAsync(jobId);
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_ShouldFail_WhenCalledByNonOwner()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(30));

        // Act
        var released = await lockService.ReleaseLockAsync(jobId, "worker-02");

        // Assert
        released.Should().BeFalse();

        var isLocked = await lockService.IsLockedAsync(jobId);
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task IsLockedAsync_ShouldReturnFalse_WhenNoLockExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        // Act
        var isLocked = await lockService.IsLockedAsync(jobId);

        // Assert
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task IsLockedAsync_ShouldReturnTrue_WhenLockExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(30));

        // Act
        var isLocked = await lockService.IsLockedAsync(jobId);

        // Assert
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task GetLockOwnerAsync_ShouldReturnOwner_WhenLockExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();
        var workerId = "test-worker-01";

        await lockService.TryAcquireLockAsync(jobId, workerId, TimeSpan.FromSeconds(30));

        // Act
        var owner = await lockService.GetLockOwnerAsync(jobId);

        // Assert
        owner.Should().Be(workerId);
    }

    [Fact]
    public async Task GetLockOwnerAsync_ShouldReturnNull_WhenNoLockExists()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        // Act
        var owner = await lockService.GetLockOwnerAsync(jobId);

        // Assert
        owner.Should().BeNull();
    }

    [Fact]
    public async Task ExtendLockAsync_ShouldExtend_WhenCalledByOwner()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();
        var workerId = "test-worker-01";

        await lockService.TryAcquireLockAsync(jobId, workerId, TimeSpan.FromSeconds(5));

        // Act
        var extended = await lockService.ExtendLockAsync(jobId, workerId, TimeSpan.FromSeconds(60));

        // Assert
        extended.Should().BeTrue();

        var isLocked = await lockService.IsLockedAsync(jobId);
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task ExtendLockAsync_ShouldFail_WhenCalledByNonOwner()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(30));

        // Act
        var extended = await lockService.ExtendLockAsync(jobId, "worker-02", TimeSpan.FromSeconds(60));

        // Assert
        extended.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ShouldSucceed_AfterLockExpires()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(1));

        // Wait for lock to expire
        await Task.Delay(1500);

        // Act
        var acquired = await lockService.TryAcquireLockAsync(jobId, "worker-02", TimeSpan.FromSeconds(30));

        // Assert
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ShouldSucceed_AfterRelease()
    {
        // Arrange
        await InitializeAsync();
        await FlushRedisAsync();

        var lockService = GetRedisLockService();
        var jobId = Guid.CreateVersion7();

        await lockService.TryAcquireLockAsync(jobId, "worker-01", TimeSpan.FromSeconds(30));
        await lockService.ReleaseLockAsync(jobId, "worker-01");

        // Act
        var acquired = await lockService.TryAcquireLockAsync(jobId, "worker-02", TimeSpan.FromSeconds(30));

        // Assert
        acquired.Should().BeTrue();
    }
}
