using FluentValidation;
using TaskFlow.Application.Commands.Users.UpdateUserCommand;

namespace TaskFlow.Application.Validators;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        // UserId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.FirstName).MaximumLength(100).WithMessage("First name cannot exceed 100 characters");
        RuleFor(x => x.LastName).MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");
        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email).EmailAddress().WithMessage("Email is not valid")
                .MaximumLength(255).WithMessage("Email cannot exceed 255 characters");
        });
    }
}
