using Milvaion.Application.Dtos.MetricReportDtos;
using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportList;

/// <summary>
/// Gets a list of metric reports, optionally filtered by metric type.
/// </summary>
public record GetMetricReportListQuery : ListRequest, IListRequestQuery<MetricReportListDto>
{
    /// <summary>
    /// Type of metric.
    /// </summary>
    public string MetricType { get; set; }
}
