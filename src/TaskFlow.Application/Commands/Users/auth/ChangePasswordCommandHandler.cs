using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.auth;

public class ChangePasswordCommandHandler : ICommandHandler<ChangePassowrdCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public ChangePasswordCommandHandler(IUnitOfWork unitOfWork, IPasswordService passwordService)
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }
    public async Task<Result<bool>> HandleAsync(ChangePassowrdCommand command, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if(user == null)
            return Result<bool>.NotFound("User not found.");
        

        if (!_passwordService.VerifyPassword(command.CurrentPassword, user.PasswordHash))  
            return Result<bool>.Validation("Current password is incorrect.");

        user.PasswordHash = _passwordService.HashPassword(command.NewPassword);
        // Signs out every session, including a stolen token and the caller's own: everyone logs in with the new password.
        user.RevokeTokens();

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        return Result<bool>.Success(true);
    }
}
