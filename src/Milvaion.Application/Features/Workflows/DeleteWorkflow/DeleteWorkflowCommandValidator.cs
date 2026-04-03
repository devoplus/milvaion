using FluentValidation;

namespace Milvaion.Application.Features.Workflows.DeleteWorkflow;

/// <summary>
/// Validator for <see cref="DeleteWorkflowCommand"/>.
/// </summary>
public sealed class DeleteWorkflowCommandValidator : AbstractValidator<DeleteWorkflowCommand>
{
    /// <inheritdoc cref="DeleteWorkflowCommandValidator"/>
    public DeleteWorkflowCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
    }
}
