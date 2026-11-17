using FluentValidation;
using TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
{
    public UpdateUserRoleCommandValidator()
    {
        // UserId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.NewRole).NotEmpty().WithMessage("Role is required")
            .Must(role => Enum.TryParse<UserRole>(role, true, out _)).WithMessage("Invalid role");
    }
}
