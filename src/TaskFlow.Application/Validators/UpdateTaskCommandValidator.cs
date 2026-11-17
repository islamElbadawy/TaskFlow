using FluentValidation;
using TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;

namespace TaskFlow.Application.Validators;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty().WithMessage("TaskId is required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
    }
}
