using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Dispatchers;

public class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query, CancellationToken cancellationToken = default
    )
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));

        var handler = _serviceProvider.GetService(handlerType);

        var handlerMethod = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResult>, TResult>.HandleAsync));

        if (handlerMethod == null)
            throw new InvalidOperationException($"Handler for {queryType.Name} not found");

        var result = handlerMethod.Invoke(handler, new Object[] { query, cancellationToken });

        if (result is Task<TResult> task)
            return await task;

        throw new InvalidOperationException($"Handler returned invalid result type");
    }
}