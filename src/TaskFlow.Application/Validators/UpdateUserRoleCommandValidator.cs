using FluentValidation;
using TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("UserId is required");
        RuleFor(x => x.NewRole).NotEmpty().WithMessage("Role is required")
            .Must(role => Enum.TryParse<UserRole>(role, true, out _)).WithMessage("Invalid role");
    }
}
