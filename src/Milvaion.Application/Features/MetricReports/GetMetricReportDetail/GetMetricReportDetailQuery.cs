using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportDetail;

/// <summary>
/// Gets metric report detail by its unique identifier.
/// </summary>
public record GetMetricReportDetailQuery : IQuery<MetricReportDetailDto>
{
    /// <summary>
    /// Id of metric report.
    /// </summary>
    public Guid Id { get; set; }
}
