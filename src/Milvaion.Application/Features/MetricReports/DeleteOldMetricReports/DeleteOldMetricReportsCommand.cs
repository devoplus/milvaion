using Milvasoft.Components.CQRS.Command;

namespace Milvaion.Application.Features.MetricReports.DeleteOldMetricReports;

/// <summary>
/// DeleteOldMetricReportsCommand is a command that represents the action of deleting old metric reports that are older than a specified number of days.
/// </summary>
public record DeleteOldMetricReportsCommand : ICommand<int>
{
    /// <summary>
    /// Deletes metric reports that are older than the specified number of days. Default is 30 days.
    /// </summary>
    public int OlderThanDays { get; set; } = 30;
}
