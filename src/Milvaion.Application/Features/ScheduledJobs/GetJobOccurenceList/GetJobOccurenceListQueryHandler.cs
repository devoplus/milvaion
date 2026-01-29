using Microsoft.EntityFrameworkCore;
using Milvaion.Application.Dtos.ScheduledJobDtos;
using Milvaion.Application.Interfaces.Redis;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using System.Linq.Expressions;
using System.Text.Json;

namespace Milvaion.Application.Features.ScheduledJobs.GetJobOccurenceList;

/// <summary>
/// Handles the job occurrence list operation.
/// </summary>
/// <param name="milvaionDbContextAccessor"></param>
/// <param name="redisStatsService"></param>
public class GetJobOccurrenceListQueryHandler(IMilvaionDbContextAccessor milvaionDbContextAccessor, IRedisStatsService redisStatsService) : IInterceptable, IListQueryHandler<GetJobOccurrenceListQuery, JobOccurrenceListDto>
{
    private readonly IMilvaionDbContextAccessor _milvaionDbContextAccessor = milvaionDbContextAccessor;
    private readonly IRedisStatsService _redisStatsService = redisStatsService;

    /// <inheritdoc/>
    public async Task<ListResponse<JobOccurrenceListDto>> Handle(GetJobOccurrenceListQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<JobOccurrence, bool>> predicate = null;
        int? manualCount = null;

        if (string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var redisStats = await _redisStatsService.GetStatisticsAsync(cancellationToken);

            manualCount = (int)redisStats.GetValueOrDefault("Total", 0);
        }

        var statusFilter = request.Filtering?.Criterias?.FirstOrDefault(i => i.FilterBy == nameof(JobOccurrence.Status));

        if (statusFilter is not null)
        {
            var redisStats = await _redisStatsService.GetStatisticsAsync(cancellationToken);
            manualCount = (int?)redisStats.GetValueOrDefault(Enum.GetName(GetValueWithCorrectType<JobOccurrenceStatus>(statusFilter.Value)), 0);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            if (Guid.TryParse(request.SearchTerm, out var guid))
                predicate = c => c.Id == guid;
            else
                predicate = c => EF.Functions.ILike(c.JobName, $"%{request.SearchTerm.Trim()}%");
        }

        var context = _milvaionDbContextAccessor.GetDbContext();

        var response = await context.Set<JobOccurrence>().ToListResponseAsync(request, JobOccurrenceListDto.Projection, manualCount, cancellationToken: cancellationToken);

        return response;
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

// TODO implement in Milvasoft.DataAccess.EfCore project
public static class EfExtensions
{
    /// <summary>
    /// ToListResponseAsync extension method for IQueryable with filtering, sorting, and pagination.
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TDto"></typeparam>
    /// <param name="query"></param>
    /// <param name="listRequest"></param>
    /// <param name="projection"></param>
    /// <param name="manualTotalCount"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ListResponse<TDto>> ToListResponseAsync<TEntity, TDto>(this IQueryable<TEntity> query,
                                                                                    ListRequest listRequest,
                                                                                    Expression<Func<TEntity, TDto>> projection,
                                                                                    int? manualTotalCount = null,
                                                                                    CancellationToken cancellationToken = default) where TEntity : class
    {
        if (query == null)
            return ListResponse<TDto>.Success();

        listRequest ??= new ListRequest();
        query = query.WithFilteringAndSorting(listRequest);

        int? totalDataCount = manualTotalCount;
        int? totalPageCount = null;

        if (listRequest.PageNumber.HasValue && listRequest.RowCount.HasValue)
        {
            if (!totalDataCount.HasValue)
                totalDataCount = await query.CountAsync(cancellationToken);

            totalPageCount = listRequest.CalculatePageCountAndCompareWithRequested(totalDataCount);

            query = query.Skip((listRequest.PageNumber.Value - 1) * listRequest.RowCount.Value).Take(listRequest.RowCount.Value);
        }

        var list = await query.Select(projection).ToListAsync(cancellationToken);

        return ListResponse<TDto>.Success(list, "Operation successful!", listRequest.PageNumber, totalPageCount, totalDataCount);
    }
}