using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Data.Seeding;

public class DataSeeder
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public DataSeeder(IUnitOfWork unitOfWork, IPasswordService passwordService)
    {
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }

    public async Task SeedAsync()
    {
        // Check if users exist
        var users = await _unitOfWork.Users.GetAllAsync();
        if (users.Any()) return;

        // Fixed GUIDs for reference
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var managerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var userId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var password = "P@ssw0rd";

        var admin = new User { Id = adminId, FirstName = "Admin", LastName = "User", Email = "admin@taskflow.dev", PasswordHash = _passwordService.HashPassword(password), Role = UserRole.Admin };
        var manager = new User { Id = managerId, FirstName = "Manager", LastName = "User", Email = "manager@taskflow.dev", PasswordHash = _passwordService.HashPassword(password), Role = UserRole.Manager };
        var regular = new User { Id = userId, FirstName = "Regular", LastName = "User", Email = "user@taskflow.dev", PasswordHash = _passwordService.HashPassword(password), Role = UserRole.User };

        await _unitOfWork.Users.AddAsync(admin);
        await _unitOfWork.Users.AddAsync(manager);
        await _unitOfWork.Users.AddAsync(regular);

        // Seed tasks
        var t1 = new TaskItem { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Title = "Setup project", Description = "Initial project setup", CreatedByUserId = adminId, AssignedToUserId = managerId, Priority = TaskPriority.High };
        var t2 = new TaskItem { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Title = "Write docs", Description = "Add README and docs", CreatedByUserId = managerId, AssignedToUserId = userId, Priority = TaskPriority.Medium };

        await _unitOfWork.TasksItems.AddAsync(t1);
        await _unitOfWork.TasksItems.AddAsync(t2);

        await _unitOfWork.SaveChangesAsync();

        // Save seed info to file — will be created by caller if needed
    }
}
