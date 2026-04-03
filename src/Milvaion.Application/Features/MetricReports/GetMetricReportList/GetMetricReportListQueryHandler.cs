using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using System.Linq.Expressions;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportList;

/// <summary>
/// Gets a list of metric reports, optionally filtered by metric type.
/// </summary>
/// <param name="metricReportRepository"></param>
public class GetMetricReportListQueryHandler(IMilvaionRepositoryBase<MetricReport> metricReportRepository) : IInterceptable, IListQueryHandler<GetMetricReportListQuery, MetricReportListDto>
{
    private readonly IMilvaionRepositoryBase<MetricReport> _metricReportRepository = metricReportRepository;

    /// <inheritdoc />
    public async Task<ListResponse<MetricReportListDto>> Handle(GetMetricReportListQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<MetricReport, bool>> predicate = null;

        if (!string.IsNullOrWhiteSpace(request.MetricType))
        {
            predicate = r => r.MetricType == request.MetricType;
        }

        var response = await _metricReportRepository.GetAllAsync(
            request,
            condition: predicate,
            projection: MetricReportListDto.Projection,
            cancellationToken: cancellationToken);

        return response;
    }
}
