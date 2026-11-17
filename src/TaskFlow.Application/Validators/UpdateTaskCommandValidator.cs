using FluentValidation;
using TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty().WithMessage("TaskId is required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
        RuleFor(x => x.Status).NotEmpty().WithMessage("Status is required")
            .IsEnumName(typeof(TaskItemStatus), caseSensitive: false).WithMessage("Status must be one of: Todo, InProgress, Done");
        RuleFor(x => x.Priority).NotEmpty().WithMessage("Priority is required")
            .IsEnumName(typeof(TaskPriority), caseSensitive: false).WithMessage("Priority must be one of: Low, Medium, High");
    }
}
