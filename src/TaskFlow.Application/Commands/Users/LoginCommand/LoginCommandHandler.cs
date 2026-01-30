
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Auth;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.LoginCommand;

public class LoginCommandHandler : ICommandHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(IUnitOfWork unitOfWork, IPasswordService passwordService, IJwtService jwtService)
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
        _jwtService = jwtService;
    }

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result<LoginResponse>.Failure("Email is required");

        if (string.IsNullOrWhiteSpace(command.Password))
            return Result<LoginResponse>.Failure("Password is required");

        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == command.Email.ToLower());

        if (user == null)
            return Result<LoginResponse>.Failure("Invalid email or password");

        if (!_passwordService.VerifyPassword(command.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure("Invalid email or password");

        var token = _jwtService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var userDto = new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: $"{user.FirstName} {user.LastName}",
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt
        );

        var response = new LoginResponse(
            Token: token,
            User: userDto,
            ExpiresAt: expiresAt
        );

        return Result<LoginResponse>.Success(response);
    }
}