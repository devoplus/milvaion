using FluentValidation;

namespace Milvaion.Application.Features.MetricReports.GetMetricReportDetail;

/// <inheritdoc />
public class GetMetricReportDetailQueryValidator : AbstractValidator<GetMetricReportDetailQuery>
{
    /// <inheritdoc />
    public GetMetricReportDetailQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Report ID is required");
    }
}
