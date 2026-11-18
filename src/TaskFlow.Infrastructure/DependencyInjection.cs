using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Repositories;
using TaskFlow.Infrastructure.Services;
using TaskFlow.Infrastructure.Services.Email;

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

        // Email: queued in memory and sent over SMTP by a background service; off unless Smtp:Enabled is true.
        services.AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailDispatcher>();


        return services;
    }
}