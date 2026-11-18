using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users;

public class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordService passwordService
    )
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }

    public async Task<Result<UserDto>> HandleAsync(
        RegisterUserCommand command, CancellationToken cancellationToken
    )
    {
        // Required fields, email format and password length are checked by RegisterUserCommandValidator.
        var email = command.Email.Trim().ToLowerInvariant();

        var existingUser = await _unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
            return Result<UserDto>.Conflict("Email already registered");

        // Create user
        var user = new User
        {
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = email,
            PasswordHash = _passwordService.HashPassword(command.Password),
            Role = UserRole.User
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: $"{user.FirstName} {user.LastName}",
            Email: user.Email,
            Role: user.Role.ToString(),
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt
        );

        return Result<UserDto>.Success(userDto);
    }
}