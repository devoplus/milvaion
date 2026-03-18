using Cronos;
using FluentValidation;

namespace Milvaion.Application.Features.Workflows.UpdateWorkflow;

/// <summary>
/// Validator for <see cref="UpdateWorkflowCommand"/>.
/// </summary>
public sealed class UpdateWorkflowCommandValidator : AbstractValidator<UpdateWorkflowCommand>
{
    /// <inheritdoc cref="UpdateWorkflowCommandValidator"/>
    public UpdateWorkflowCommandValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CronExpression)
            .MaximumLength(100)
            .Must(expr =>
            {
                if (string.IsNullOrWhiteSpace(expr))
                    return true;
                try
                {
                    CronExpression.Parse(expr, CronFormat.IncludeSeconds);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("Invalid cron expression.");
    }
}
