using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.MetricReports.DeleteMetricReport;

/// <summary>
/// DeleteMetricReportCommand is a command that represents the action of deleting a metric report.
/// </summary>
public record DeleteMetricReportCommand : ICommand<Guid>
{
    /// <summary>
    /// Id of the metric report to be deleted.
    /// </summary>
    public Guid Id { get; set; }
}
