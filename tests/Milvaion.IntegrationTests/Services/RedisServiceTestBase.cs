using Microsoft.Extensions.DependencyInjection;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Infrastructure.Services.Redis;
using Milvaion.IntegrationTests.TestBase;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.Services;

/// <summary>
/// Base class for Redis service integration tests.
/// Provides access to Redis services and cleanup utilities.
/// </summary>
public abstract class RedisServiceTestBase(CustomWebApplicationFactory factory, ITestOutputHelper output) : IntegrationTestBase(factory, output)
{
    /// <summary>
    /// Gets the Redis connection multiplexer.
    /// </summary>
    protected IConnectionMultiplexer GetRedisConnection() => _serviceProvider.GetRequiredService<IConnectionMultiplexer>();

    /// <summary>
    /// Gets the Redis database instance.
    /// </summary>
    protected IDatabase GetRedisDatabase() => GetRedisConnection().GetDatabase();

    /// <summary>
    /// Gets the Redis lock service.
    /// </summary>
    protected IRedisLockService GetRedisLockService() => _serviceProvider.GetRequiredService<IRedisLockService>();

    /// <summary>
    /// Gets the Redis scheduler service.
    /// </summary>
    protected IRedisSchedulerService GetRedisSchedulerService() => _serviceProvider.GetRequiredService<IRedisSchedulerService>();

    /// <summary>
    /// Gets the Redis stats service.
    /// </summary>
    protected IRedisStatsService GetRedisStatsService() => _serviceProvider.GetRequiredService<IRedisStatsService>();

    /// <summary>
    /// Gets the Redis worker service.
    /// </summary>
    protected IRedisWorkerService GetRedisWorkerService() => _serviceProvider.GetRequiredService<IRedisWorkerService>();

    /// <summary>
    /// Gets the Redis cancellation service.
    /// </summary>
    protected IRedisCancellationService GetRedisCancellationService() => _serviceProvider.GetRequiredService<IRedisCancellationService>();

    /// <summary>
    /// Gets the Redis connection service.
    /// </summary>
    protected RedisConnectionService GetRedisConnectionService() => _serviceProvider.GetRequiredService<RedisConnectionService>();

    /// <summary>
    /// Deletes all keys in the current Redis database to ensure clean test state.
    /// Uses SCAN + DEL instead of FLUSHDB to avoid requiring admin mode.
    /// </summary>
    protected async Task FlushRedisAsync()
    {
        var db = GetRedisDatabase();
        var server = GetRedisConnection().GetServers().First();

        var keys = new List<RedisKey>();

        await foreach (var key in server.KeysAsync(database: db.Database, pattern: "*", pageSize: 500))
            keys.Add(key);

        if (keys.Count > 0)
            await db.KeyDeleteAsync([.. keys]);
    }
}
