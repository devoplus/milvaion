using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Features.MetricReports.GetLatestMetricReport;

/// <summary>
/// Gets latest metric report for a specified metric type.
/// </summary>
/// <param name="metricReportRepository"></param>
public class GetLatestMetricReportQueryHandler(IMilvaionRepositoryBase<MetricReport> metricReportRepository) : IInterceptable, IQueryHandler<GetLatestMetricReportQuery, MetricReportDetailDto>
{
    private readonly IMilvaionRepositoryBase<MetricReport> _metricReportRepository = metricReportRepository;

    /// <inheritdoc />
    public async Task<Response<MetricReportDetailDto>> Handle(GetLatestMetricReportQuery request, CancellationToken cancellationToken)
    {
        var reports = await _metricReportRepository.GetAllAsync(
            condition: r => r.MetricType == request.MetricType,
            projection: MetricReportDetailDto.Projection,
            cancellationToken: cancellationToken);

        var report = reports?.OrderByDescending(r => r.GeneratedAt).FirstOrDefault();

        if (report == null)
            return Response<MetricReportDetailDto>.Error(default, "Metric report not found for the specified type");

        return Response<MetricReportDetailDto>.Success(report);
    }
}
