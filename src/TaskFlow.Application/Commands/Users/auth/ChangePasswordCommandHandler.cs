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
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId);
        if(user == null)
            return Result<bool>.Failure("User not found.");
        

        if (!_passwordService.VerifyPassword(command.CurrentPassword, user.PasswordHash))  
            return Result<bool>.Failure("Current password is incorrect.");
        
        if(string.IsNullOrEmpty(command.NewPassword) || command.NewPassword.Length < 6)
            return Result<bool>.Failure("New password must be at least 6 characters long.");

        user.PasswordHash = _passwordService.HashPassword(command.NewPassword);

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();


        return Result<bool>.Success(true);
    }
}
