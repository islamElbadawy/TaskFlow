using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users;

public class AdminCreateUserCommandHandler : ICommandHandler<AdminCreateUserCommand, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public AdminCreateUserCommandHandler(IUnitOfWork unitOfWork, IPasswordService passwordService)
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }

    public async Task<Result<UserDto>> HandleAsync(
        AdminCreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var existing = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing != null)
            return Result<UserDto>.Conflict("Email already registered");

        if (!Enum.TryParse<UserRole>(command.Role, ignoreCase: true, out var parsedRole) || !Enum.IsDefined(parsedRole))
            return Result<UserDto>.Validation("Role must be User, Manager, or Admin.");

        var user = new User
        {
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            Email = email,
            PasswordHash = _passwordService.HashPassword(command.Password),
            Role = parsedRole,
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<UserDto>.Success(new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: $"{user.FirstName} {user.LastName}",
            Email: user.Email,
            Role: user.Role.ToString(),
            IsActive: user.IsActive,
            CreatedAt: user.CreatedAt));
    }
}
