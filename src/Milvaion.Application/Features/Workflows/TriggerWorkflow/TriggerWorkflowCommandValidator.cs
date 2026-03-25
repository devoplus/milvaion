using FluentValidation;

namespace Milvaion.Application.Features.Workflows.TriggerWorkflow;

/// <summary>
/// Validator for <see cref="TriggerWorkflowCommand"/>.
/// </summary>
public sealed class TriggerWorkflowCommandValidator : AbstractValidator<TriggerWorkflowCommand>
{
    /// <inheritdoc cref="TriggerWorkflowCommandValidator"/>
    public TriggerWorkflowCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
    }
}
