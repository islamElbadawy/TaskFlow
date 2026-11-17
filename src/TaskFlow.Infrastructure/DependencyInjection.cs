using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Repositories;
using TaskFlow.Infrastructure.Services;

namespace TaskFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration
    )
    {
        // Database (PostgreSQL). The connection string's password comes from user-secrets / environment.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        services.AddDbContext<ApplicationDbContext>(options => ApplicationDbContext.Configure(options, connectionString));


        // Repository pattern
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();

        // Services
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<Data.Seeding.DataSeeder>();
        services.AddScoped<TaskFlow.Application.Common.Interfaces.INotificationService, NotificationService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();


        return services;
    }
}