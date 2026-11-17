using FluentValidation;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class ChangeTaskStatusCommandValidator : AbstractValidator<ChangeTaskStatusCommand>
{
    public ChangeTaskStatusCommandValidator()
    {
        // TaskId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required")
            .IsEnumName(typeof(TaskItemStatus), caseSensitive: false).WithMessage("Status must be one of: Todo, InProgress, Done");
    }
}
