using Microsoft.Extensions.DependencyInjection;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Constants;
using Milvaion.Infrastructure.Persistence.Context;
using Milvasoft.Core.Abstractions;
using Milvasoft.JobScheduling;

namespace Milvaion.Infrastructure.InternalJobs;

/// <summary>
/// Syncs redis statistics from database periodically.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
public class RedisStatSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory) : MilvaCronJobService(scheduleConfig)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <inheritdoc/>
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<IMilvaLogger>();

        try
        {
            var context = scope.ServiceProvider.GetService<MilvaionDbContext>();
            var redisStatsService = scope.ServiceProvider.GetService<IRedisStatsService>();

            await redisStatsService.SyncCountersFromDatabaseAsync(context, cancellationToken);

            logger.Information(LogTemplate.JobExecuted, nameof(RedisStatSyncJob));
        }
        catch (Exception ex)
        {
            logger.Error(ex, LogTemplate.JobException, nameof(RedisStatSyncJob), ex.Message);
        }
    }
}