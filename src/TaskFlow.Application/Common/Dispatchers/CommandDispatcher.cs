using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Dispatchers;

public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResult> DispatchAsync<TResult>(ICommand<TResult> command,
        CancellationToken cancellationToken = default)
    {
        var commandType = command.GetType();

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(commandType, typeof(TResult));

        var handler = _serviceProvider.GetService(handlerType);

        var handlerMethod = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResult>, TResult>.HandleAsync));

        if (handlerMethod == null)
            throw new InvalidOperationException($"Handler for {commandType.Name} not found");

        var result = handlerMethod.Invoke(handler, new Object[] { command, cancellationToken });

        if (result is Task<TResult> task)
            return await task;

        throw new InvalidOperationException($"Handler returned invalid result type");
    }
}