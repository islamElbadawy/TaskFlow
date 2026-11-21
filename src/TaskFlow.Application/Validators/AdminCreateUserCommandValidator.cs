using FluentValidation;
using TaskFlow.Application.Commands.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class AdminCreateUserCommandValidator : AbstractValidator<AdminCreateUserCommand>
{
    public AdminCreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).WithMessage("Password must be at least 6 characters");
        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<UserRole>(r, ignoreCase: true, out _))
            .WithMessage("Role must be User, Manager, or Admin");
    }
}
