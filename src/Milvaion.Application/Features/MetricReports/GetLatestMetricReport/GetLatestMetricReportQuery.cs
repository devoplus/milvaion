using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;

namespace Milvaion.Application.Features.MetricReports.GetLatestMetricReport;

/// <summary>
/// Gets latest metric report for a specified metric type.
/// </summary>
public record GetLatestMetricReportQuery : IQuery<MetricReportDetailDto>
{
    /// <summary>
    /// Type of metric.
    /// </summary>
    public string MetricType { get; set; }
}
