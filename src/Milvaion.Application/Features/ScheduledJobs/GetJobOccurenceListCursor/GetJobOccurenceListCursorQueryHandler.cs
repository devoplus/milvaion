using Microsoft.EntityFrameworkCore;
using Milvaion.Application.Dtos.ScheduledJobDtos;
using Milvaion.Application.Interfaces.Redis;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using System.Linq.Expressions;
using System.Text.Json;

namespace Milvaion.Application.Features.ScheduledJobs.GetJobOccurenceListCursor;

/// <summary>
/// Handles the job occurrence list operation.
/// </summary>
/// <param name="milvaionDbContextAccessor"></param>
/// <param name="redisStatsService"></param>
public class GetJobOccurenceListCursorQueryHandler(IMilvaionDbContextAccessor milvaionDbContextAccessor, IRedisStatsService redisStatsService) : IInterceptable, IQueryHandler<GetJobOccurenceListCursorQuery, CursorListResponse<JobOccurrenceListDto>>
{
    private readonly IMilvaionDbContextAccessor _milvaionDbContextAccessor = milvaionDbContextAccessor;
    private readonly IRedisStatsService _redisStatsService = redisStatsService;

    /// <inheritdoc/>
    public async Task<Response<CursorListResponse<JobOccurrenceListDto>>> Handle(GetJobOccurenceListCursorQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<JobOccurrence, bool>> predicate = null;
        int? manualCount = null;

        var jobIdFilter = request.Filtering?.Criterias?.FirstOrDefault(i => i.FilterBy == nameof(JobOccurrence.JobId));
        var statusFilter = request.Filtering?.Criterias?.FirstOrDefault(i => i.FilterBy == nameof(JobOccurrence.Status));

        // Only use Redis stats when there are no filters that would make it inaccurate (e.g., JobId filter)
        var canUseRedisStats = jobIdFilter is null;

        if (canUseRedisStats)
        {
            if (statusFilter is not null)
            {
                var redisStats = await _redisStatsService.GetStatisticsAsync(cancellationToken);
                manualCount = (int?)redisStats.GetValueOrDefault(Enum.GetName(GetValueWithCorrectType<JobOccurrenceStatus>(statusFilter.Value)), 0);
            }
            else if (string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var redisStats = await _redisStatsService.GetStatisticsAsync(cancellationToken);
                manualCount = (int)redisStats.GetValueOrDefault("Total", 0);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            if (Guid.TryParse(request.SearchTerm, out var guid))
                predicate = c => c.Id == guid;
            else
                predicate = c => EF.Functions.ILike(c.JobName, $"%{request.SearchTerm.Trim()}%");
        }

        var context = _milvaionDbContextAccessor.GetDbContext();

        var response = await context.Set<JobOccurrence>().ToCursorListResponseAsync(request, JobOccurrenceListDto.Projection, cancellationToken: cancellationToken);

        response.TotalDataCount = manualCount ?? response.TotalDataCount;

        return Response<CursorListResponse<JobOccurrenceListDto>>.Success(response);
    }

    /// <summary>
    /// Gets the value with the correct type from the object.
    /// </summary>
    /// <typeparam name="TActualType"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TActualType GetValueWithCorrectType<TActualType>(object value)
    {
        try
        {
            if (value is TActualType typedValue)
                return typedValue;

            if (value is JsonElement jsonElement)
                return jsonElement.Deserialize<TActualType>();

            // Fallback: Try Convert.ChangeType for simple types like int, bool, etc.
            return (TActualType)Convert.ChangeType(value, typeof(TActualType));
        }
        catch
        {
            return default;
        }
    }
}