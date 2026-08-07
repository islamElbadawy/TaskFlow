using FluentValidation;
using TaskFlow.Application.Commands.Users.auth;

namespace TaskFlow.Application.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePassowrdCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("UserId is required");
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Current password is required");
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage("New password is required").MinimumLength(6).WithMessage("New password must be at least 6 characters");
    }
}
