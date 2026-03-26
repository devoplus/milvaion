using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportDetail;

/// <summary>
/// Gets metric report detail.
/// </summary>
/// <param name="metricReportRepository"></param>
public class GetMetricReportDetailQueryHandler(IMilvaionRepositoryBase<MetricReport> metricReportRepository) : IInterceptable, IQueryHandler<GetMetricReportDetailQuery, MetricReportDetailDto>
{
    private readonly IMilvaionRepositoryBase<MetricReport> _metricReportRepository = metricReportRepository;

    /// <inheritdoc />
    public async Task<Response<MetricReportDetailDto>> Handle(GetMetricReportDetailQuery request, CancellationToken cancellationToken)
    {
        var report = await _metricReportRepository.GetFirstOrDefaultAsync(
            condition: r => r.Id == request.Id,
            projection: MetricReportDetailDto.Projection,
            cancellationToken: cancellationToken);

        if (report == null)
            return Response<MetricReportDetailDto>.Error(default, "Metric report not found");

        return Response<MetricReportDetailDto>.Success(report);
    }
}
