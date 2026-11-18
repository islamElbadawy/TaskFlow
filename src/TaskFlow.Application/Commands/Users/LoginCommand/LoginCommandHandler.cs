
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

    /// <summary>A hash of a random password with the same cost as real ones, checked when the email is unknown.</summary>
    private string DummyHash => _dummyHash ??= _passwordService.HashPassword(Guid.NewGuid().ToString());
    private static string? _dummyHash;

    public async Task<Result<LoginResponse>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        // Email and password presence is checked by LoginCommandValidator (run by the dispatcher).
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Unknown email, wrong password and deactivated account all get the same message,
        // so the response doesn't reveal which accounts exist or are disabled.
        // An unknown email still pays for one hash check, so the response time doesn't reveal it either.
        if (user == null)
        {
            _passwordService.VerifyPassword(command.Password, DummyHash);
            return Result<LoginResponse>.Unauthorized("Invalid email or password");
        }

        if (!_passwordService.VerifyPassword(command.Password, user.PasswordHash))
            return Result<LoginResponse>.Unauthorized("Invalid email or password");

        if (!user.IsActive)
            return Result<LoginResponse>.Unauthorized("Invalid email or password");

        var (token, expiresAt) = _jwtService.GenerateToken(user);

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

        var response = new LoginResponse(
            Token: token,
            User: userDto,
            ExpiresAt: expiresAt
        );

        return Result<LoginResponse>.Success(response);
    }
}