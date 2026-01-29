using Milvaion.Application.Dtos.AdminDtos;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Interfaces;

/// <summary>
/// Implementation of admin service.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IAdminService"/> class.
/// </remarks>
public interface IAdminService : IInterceptable
{
    /// <summary>
    /// Gets queue statistics for all queues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue statistics</returns>
    public Task<Response<List<QueueStats>>> GetQueueStatsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets detailed information about a specific queue.
    /// </summary>
    /// <param name="queueName">Queue name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue depth information</returns>
    public Task<Response<QueueDepthInfo>> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets system health overview including dispatcher status.
    /// </summary>
    /// <returns>System health information</returns>
    public Task<Response<SystemHealthInfo>> GetSystemHealthAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Emergency stop - Disables the job dispatcher at runtime.
    /// </summary>
    /// <param name="reason">Reason for emergency stop</param>
    /// <returns>Success response</returns>
    public IResponse EmergencyStop(string reason);

    /// <summary>
    /// Resume operations - Enables the job dispatcher at runtime.
    /// </summary>
    /// <returns>Success response</returns>
    public IResponse ResumeOperations();

    /// <summary>
    /// Gets job statistics grouped by status.
    /// </summary>
    /// <returns>Job statistics</returns>
    public Task<Response<JobStatistics>> GetJobStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets Redis circuit breaker statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Circuit breaker statistics</returns>
    public Response<RedisCircuitBreakerStatsDto> GetRedisCircuitBreakerStats(CancellationToken cancellationToken);

    /// <summary>
    /// Gets database statistics including table sizes, occurrence growth, and large occurrences.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Database statistics</returns>
    public Task<Response<DatabaseStatisticsDto>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets top tables by size.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Table sizes</returns>
    public Task<Response<List<TableSizeDto>>> GetTableSizesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets index efficiency statistics (unused/underutilized indexes).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Index efficiency statistics</returns>
    public Task<Response<IndexEfficiencyDto>> GetIndexEfficiencyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets database cache hit ratio.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache hit ratio statistics</returns>
    public Task<Response<CacheHitRatioDto>> GetCacheHitRatioAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets table bloat detection (VACUUM recommendation).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Table bloat statistics</returns>
    public Task<Response<TableBloatDto>> GetTableBloatAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets background service memory diagnostics.
    /// </summary>
    /// <returns>Database statistics</returns>
    public Response<AggregatedMemoryStats> GetBackgroundServiceMemoryDiagnostics();
}
