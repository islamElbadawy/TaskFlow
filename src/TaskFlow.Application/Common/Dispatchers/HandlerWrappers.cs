using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Dispatchers;

// Typed bridges from ICommand<TResult>/IQuery<TResult> to their handlers. The dispatchers create one per request type
// (the only reflection) and cache it; after that, validating and calling the handler are ordinary generic calls.

internal abstract class CommandHandlerWrapper<TResult>
{
    public abstract Task<TResult> HandleAsync(ICommand<TResult> command, IServiceProvider services,
        CancellationToken cancellationToken);
}

internal sealed class CommandHandlerWrapper<TCommand, TResult> : CommandHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    public override async Task<TResult> HandleAsync(ICommand<TResult> command, IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var request = (TCommand)command;

        var errors = await RequestValidation.ValidateAsync(request, services, cancellationToken);
        if (errors is not null)
            return ValidationFailureResult<TResult>.Create(errors);

        var handler = services.GetService<ICommandHandler<TCommand, TResult>>()
            ?? throw new InvalidOperationException(
                $"No handler is registered for {typeof(TCommand).Name}. AddApplication registers every class that " +
                $"implements ICommandHandler<{typeof(TCommand).Name}, {typeof(TResult).Name}>.");

        return await handler.HandleAsync(request, cancellationToken);
    }
}

internal abstract class QueryHandlerWrapper<TResult>
{
    public abstract Task<TResult> HandleAsync(IQuery<TResult> query, IServiceProvider services,
        CancellationToken cancellationToken);
}

internal sealed class QueryHandlerWrapper<TQuery, TResult> : QueryHandlerWrapper<TResult>
    where TQuery : IQuery<TResult>
{
    public override async Task<TResult> HandleAsync(IQuery<TResult> query, IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var request = (TQuery)query;

        var errors = await RequestValidation.ValidateAsync(request, services, cancellationToken);
        if (errors is not null)
            return ValidationFailureResult<TResult>.Create(errors);

        var handler = services.GetService<IQueryHandler<TQuery, TResult>>()
            ?? throw new InvalidOperationException(
                $"No handler is registered for {typeof(TQuery).Name}. AddApplication registers every class that " +
                $"implements IQueryHandler<{typeof(TQuery).Name}, {typeof(TResult).Name}>.");

        return await handler.HandleAsync(request, cancellationToken);
    }
}
