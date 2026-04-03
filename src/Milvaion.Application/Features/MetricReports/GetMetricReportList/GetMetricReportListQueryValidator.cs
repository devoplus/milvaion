using FluentValidation;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportList;

/// <inheritdoc />
public class GetMetricReportListQueryValidator : AbstractValidator<GetMetricReportListQuery>
{
    /// <inheritdoc />
    public GetMetricReportListQueryValidator()
    {
        RuleFor(x => x.RowCount)
            .GreaterThan(0).WithMessage("Row count must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Row count cannot exceed 1000");
    }
}
