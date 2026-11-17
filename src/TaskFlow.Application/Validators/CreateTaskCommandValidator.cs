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
        RuleFor(x => x.CreatedById).NotEmpty().WithMessage("CreatedById is required");
    }
}
