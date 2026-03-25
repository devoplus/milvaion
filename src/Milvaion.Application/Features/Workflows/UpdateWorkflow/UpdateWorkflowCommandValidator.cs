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
        RuleFor(x => x.Steps).NotEmpty().WithMessage("Workflow must have at least one step.");
        RuleFor(x => x.TimeoutSeconds).GreaterThan(0).When(x => x.TimeoutSeconds.HasValue).WithMessage("TimeoutSeconds must be positive.");
        RuleFor(x => x.MaxStepRetries).GreaterThanOrEqualTo(0).WithMessage("MaxStepRetries cannot be negative.");
        RuleFor(x => x.Edges)
            .Must(edges => edges == null || edges.All(e => !string.IsNullOrWhiteSpace(e.SourceTempId) && !string.IsNullOrWhiteSpace(e.TargetTempId)))
            .WithMessage("Each edge must define source and target temp ids.");
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

        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.StepName).NotEmpty().MaximumLength(200);
            step.RuleFor(s => s.TempId).NotEmpty().WithMessage("Each step must have a TempId for edge referencing.");
            step.RuleFor(s => s.JobId)
                .NotEmpty()
                .When(s => s.NodeType == WorkflowNodeType.Task)
                .WithMessage("Task nodes must have a JobId.");
        });
    }
}
