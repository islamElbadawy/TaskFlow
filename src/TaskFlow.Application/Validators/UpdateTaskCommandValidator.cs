using FluentValidation;
using TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        // TaskId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required").MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required")
            .IsEnumName(typeof(TaskItemStatus), caseSensitive: false).WithMessage("Status must be one of: Todo, InProgress, Done");
        RuleFor(x => x.Priority).NotEmpty().WithMessage("Priority is required")
            .IsEnumName(typeof(TaskPriority), caseSensitive: false).WithMessage("Priority must be one of: Low, Medium, High");
    }
}
