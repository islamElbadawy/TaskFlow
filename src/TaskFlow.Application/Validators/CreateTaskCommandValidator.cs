using FluentValidation;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required").MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.Priority).NotEmpty().WithMessage("Priority is required")
            .IsEnumName(typeof(TaskPriority), caseSensitive: false).WithMessage("Priority must be one of: Low, Medium, High");
        // CreatedById comes from the caller's token (set by the controller after model validation), so it is not
        // validated here; requiring it made every request without it fail with 400.
    }
}
