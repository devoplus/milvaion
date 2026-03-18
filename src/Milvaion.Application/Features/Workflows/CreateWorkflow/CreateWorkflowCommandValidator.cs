using FluentValidation;

namespace Milvaion.Application.Features.Workflows.CreateWorkflow;

/// <summary>
/// Validator for <see cref="CreateWorkflowCommand"/>.
/// </summary>
public sealed class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    /// <inheritdoc cref="CreateWorkflowCommandValidator"/>
    public CreateWorkflowCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("Workflow must have at least one step.");
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.JobId).NotEmpty();
            step.RuleFor(s => s.StepName).NotEmpty().MaximumLength(200);
            step.RuleFor(s => s.TempId).NotEmpty().WithMessage("Each step must have a TempId for dependency referencing.");
        });
    }
}
