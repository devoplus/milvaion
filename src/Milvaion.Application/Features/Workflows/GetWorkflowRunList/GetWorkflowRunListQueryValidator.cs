using FluentValidation;

namespace Milvaion.Application.Features.Workflows.GetWorkflowRunList;

/// <summary>
/// Validator for <see cref="GetWorkflowRunListQuery"/>.
/// </summary>
public sealed class GetWorkflowRunListQueryValidator : AbstractValidator<GetWorkflowRunListQuery>
{
    /// <inheritdoc cref="GetWorkflowRunListQueryValidator"/>
    public GetWorkflowRunListQueryValidator()
    {
    }
}
