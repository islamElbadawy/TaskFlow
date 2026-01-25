using Microsoft.AspNetCore.Identity;
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
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result<UserDto>.Failure("Email is required");

        var existingUser = _unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email);

        if (existingUser != null)
            return Result<UserDto>.Failure("Email already registered");

        // Validate password
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 6)
            return Result<UserDto>.Failure("Password must be at least 6 characters");

        // Create user
        var user = new User
        {
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email.ToLower(),
            PasswordHash = _passwordService.HashPassword(command.Password),
            Role = UserRole.User
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

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