using FluentValidation;

namespace Milvaion.Application.Features.MetricReports.DeleteMetricReport;

/// <inheritdoc />
public class DeleteMetricReportCommandValidator : AbstractValidator<DeleteMetricReportCommand>
{
    /// <inheritdoc />
    public DeleteMetricReportCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Report ID is required");
    }
}
