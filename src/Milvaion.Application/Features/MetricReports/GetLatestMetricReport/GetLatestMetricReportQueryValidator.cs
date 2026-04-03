using FluentValidation;

namespace Milvaion.Application.Features.MetricReports.GetLatestMetricReport;

/// <inheritdoc />
public class GetLatestMetricReportQueryValidator : AbstractValidator<GetLatestMetricReportQuery>
{
    /// <inheritdoc />
    public GetLatestMetricReportQueryValidator()
    {
        RuleFor(x => x.MetricType)
            .NotEmpty().WithMessage("Metric type is required");
    }
}
