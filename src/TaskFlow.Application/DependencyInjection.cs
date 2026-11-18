using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Dispatchers;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
using TaskFlow.Application.Common.Reminders;

namespace TaskFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register Dispatchers
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Who gets notified about task/comment events (stages notifications for the handler's SaveChanges)
        services.AddScoped<TaskNotifications>();

        // Deadline reminders (run by the background job and POST /api/reminders/run)
        services.AddScoped<DeadlineReminderService>();

        // Auto-register all command handlers
        RegisterHandlers(services, typeof(ICommandHandler<,>));

        // Auto-register all query handlers
        RegisterHandlers(services, typeof(IQueryHandler<,>));

        // Add FluentValidation validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }


    private static void RegisterHandlers(IServiceCollection services, Type handlerInterface)
    {
        var handlers = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces(), (type, interfaceType) => new { type, interfaceType })
            .Where(t => t.interfaceType.IsGenericType && t.interfaceType.GetGenericTypeDefinition() == handlerInterface)
            .ToList();
        foreach (var handler in handlers)
        {
            services.AddScoped(handler.interfaceType, handler.type);
        }
    }
}