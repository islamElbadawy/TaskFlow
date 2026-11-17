using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.UpdateUserCommand;

public class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand,Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);

        if (user == null)
            return Result<UserDto>.Failure("User not found");

        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var email = command.Email.Trim().ToLowerInvariant();

            // Another user (not this one) already owns the email?
            var existingUser =
                await _unitOfWork.Users.FirstOrDefaultAsync(u =>
                    u.Email == email && u.Id != command.UserId, cancellationToken);

            if (existingUser != null)
                return Result<UserDto>.Failure("Email already exists");

            user.Email = email;
        }

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: $"{user.FirstName} {user.LastName}",
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt
        );

        return Result<UserDto>.Success(userDto);
    }
}