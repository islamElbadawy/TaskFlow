using FluentValidation;
using TaskFlow.Application.Commands.Users.SetUserActiveCommand;

namespace TaskFlow.Application.Validators;

public class SetUserActiveCommandValidator : AbstractValidator<SetUserActiveCommand>
{
    public SetUserActiveCommandValidator()
    {
        // UserId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.IsActive).NotNull().WithMessage("isActive is required (true or false)");
    }
}
