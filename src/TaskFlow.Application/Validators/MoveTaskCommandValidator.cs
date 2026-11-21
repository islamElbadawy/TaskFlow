using FluentValidation;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class MoveTaskCommandValidator : AbstractValidator<MoveTaskCommand>
{
    public MoveTaskCommandValidator()
    {
        RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required")
            .IsEnumName(typeof(TaskItemStatus), caseSensitive: false).WithMessage("Status must be one of: Todo, InProgress, Done");

        // TaskId is set from the route before the dispatcher runs this validator.
        RuleFor(x => x.BeforeTaskId).NotEqual(x => x.TaskId).When(x => x.BeforeTaskId.HasValue)
            .WithMessage("A task can't be placed before itself");
    }
}
