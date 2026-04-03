using FluentValidation;

namespace Milvaion.Application.Features.Workflows.GetWorkflowList;

/// <summary>
/// Validator for <see cref="GetWorkflowListQuery"/>.
/// </summary>
public sealed class GetWorkflowListQueryValidator : AbstractValidator<GetWorkflowListQuery>
{
    /// <inheritdoc cref="GetWorkflowListQueryValidator"/>
    public GetWorkflowListQueryValidator()
    {
    }
}
