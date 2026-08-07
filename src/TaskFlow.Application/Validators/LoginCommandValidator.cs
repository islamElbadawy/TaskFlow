using FluentValidation;
using TaskFlow.Application.Commands.Users.LoginCommand;

namespace TaskFlow.Application.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress().WithMessage("Email is not valid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required");
    }
}
