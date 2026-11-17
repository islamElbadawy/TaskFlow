using FluentValidation;
using TaskFlow.Application.Commands.Users.UpdateUserCommand;

namespace TaskFlow.Application.Validators;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        // UserId comes from the route (set by the controller after model validation), so it is not validated here.
        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email).EmailAddress().WithMessage("Email is not valid");
        });
    }
}
