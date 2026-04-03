using FluentValidation;

namespace Milvaion.Application.Features.Workflows.GetWorkflowDetail;

/// <summary>
/// Validator for <see cref="GetWorkflowDetailQuery"/>.
/// </summary>
public sealed class GetWorkflowDetailQueryValidator : AbstractValidator<GetWorkflowDetailQuery>
{
    /// <inheritdoc cref="GetWorkflowDetailQueryValidator"/>
    public GetWorkflowDetailQueryValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
    }
}
