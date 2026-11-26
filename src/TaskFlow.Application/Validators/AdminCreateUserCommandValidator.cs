using FluentValidation;
using TaskFlow.Application.Commands.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class AdminCreateUserCommandValidator : AbstractValidator<AdminCreateUserCommand>
{
    public AdminCreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).WithMessage("Password must be at least 6 characters");
        RuleFor(x => x.Role)
            .Must(r => Enum.TryParse<UserRole>(r, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Role must be User, Manager, or Admin");
    }
}
