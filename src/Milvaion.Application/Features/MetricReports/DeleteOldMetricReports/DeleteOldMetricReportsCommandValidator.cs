using FluentValidation;

namespace Milvaion.Application.Features.MetricReports.DeleteOldMetricReports;

/// <inheritdoc />
public class DeleteOldMetricReportsCommandValidator : AbstractValidator<DeleteOldMetricReportsCommand>
{
    /// <inheritdoc />
    public DeleteOldMetricReportsCommandValidator()
    {
        RuleFor(x => x.OlderThanDays)
            .GreaterThan(0).WithMessage("OlderThanDays must be greater than 0")
            .LessThanOrEqualTo(365).WithMessage("OlderThanDays cannot exceed 365 days");
    }
}
