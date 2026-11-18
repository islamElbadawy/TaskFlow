using System.Collections.Concurrent;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Dispatchers;

/// <summary>Validates a command with its FluentValidation validators (400 with the field errors when invalid), then
/// runs its handler. Scoped: handlers and validators come from the request's scope.</summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private static readonly ConcurrentDictionary<(Type Command, Type Result), object> Wrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wrapper = (CommandHandlerWrapper<TResult>)Wrappers.GetOrAdd((command.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(CommandHandlerWrapper<,>).MakeGenericType(key.Command, key.Result))!);

        return wrapper.HandleAsync(command, _serviceProvider, cancellationToken);
    }
}
