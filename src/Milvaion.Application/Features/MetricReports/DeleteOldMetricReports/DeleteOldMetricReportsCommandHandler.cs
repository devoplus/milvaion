using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Application.Features.MetricReports.DeleteOldMetricReports;

/// <summary>
/// Deletes old metric reports that are older than a specified number of days.
/// </summary>
/// <param name="metricReportRepository"></param>
public class DeleteOldMetricReportsCommandHandler(IMilvaionRepositoryBase<MetricReport> metricReportRepository) : IInterceptable, ICommandHandler<DeleteOldMetricReportsCommand, int>
{
    private readonly IMilvaionRepositoryBase<MetricReport> _metricReportRepository = metricReportRepository;

    /// <inheritdoc />
    public async Task<Response<int>> Handle(DeleteOldMetricReportsCommand request, CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-request.OlderThanDays);

        var oldReports = await _metricReportRepository.GetAllAsync(condition: r => r.GeneratedAt < cutoffDate,
                                                                   projection: r => r,
                                                                   cancellationToken: cancellationToken);

        if (oldReports?.Count > 0)
            await _metricReportRepository.BulkDeleteAsync([.. oldReports], cancellationToken: cancellationToken);

        return Response<int>.Success(oldReports?.Count ?? 0);
    }
}
