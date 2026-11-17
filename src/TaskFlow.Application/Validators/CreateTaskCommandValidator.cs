using FluentValidation;
using TaskFlow.Application.Commands.Tasks;

namespace TaskFlow.Application.Validators;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
        RuleFor(x => x.Description).NotNull();
        RuleFor(x => x.Priority).NotEmpty().WithMessage("Priority is required");
        // CreatedById comes from the caller's token (set by the controller after model validation), so it is not
        // validated here; requiring it made every request without it fail with 400.
    }
}
