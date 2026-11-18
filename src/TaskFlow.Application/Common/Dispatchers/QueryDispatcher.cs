using System.Collections.Concurrent;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Dispatchers;

/// <summary>Validates a query with its FluentValidation validators (400 with the field errors when invalid), then
/// runs its handler. Scoped: handlers and validators come from the request's scope.</summary>
public sealed class QueryDispatcher : IQueryDispatcher
{
    private static readonly ConcurrentDictionary<(Type Query, Type Result), object> Wrappers = new();

    private readonly IServiceProvider _serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wrapper = (QueryHandlerWrapper<TResult>)Wrappers.GetOrAdd((query.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(QueryHandlerWrapper<,>).MakeGenericType(key.Query, key.Result))!);

        return wrapper.HandleAsync(query, _serviceProvider, cancellationToken);
    }
}
