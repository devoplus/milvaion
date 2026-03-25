using FluentValidation;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunDetail;

/// <summary>
/// Validator for <see cref="GetWorkflowRunDetailQuery"/>.
/// </summary>
public sealed class GetWorkflowRunDetailQueryValidator : AbstractValidator<GetWorkflowRunDetailQuery>
{
    /// <inheritdoc cref="GetWorkflowRunDetailQueryValidator"/>
    public GetWorkflowRunDetailQueryValidator()
    {
        RuleFor(x => x.RunId).NotEmpty();
    }
}
