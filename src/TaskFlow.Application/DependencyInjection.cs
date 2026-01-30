using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Dispatchers;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register Dispatchers
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Auto-register all command handlers
        RegisterHandlers(services, typeof(ICommandHandler<,>));

        // Auto-register all query handlers
        RegisterHandlers(services, typeof(IQueryHandler<,>));

        // Add FluentValidation validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Add AutoMapper (if you want to use it)
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

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