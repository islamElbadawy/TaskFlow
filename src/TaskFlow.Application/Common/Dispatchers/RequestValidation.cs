using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Common.Dispatchers;

/// <summary>
/// The validation step of both dispatchers: runs every <see cref="IValidator{T}"/> registered for the request, after the
/// controller has filled in the route and server-owned fields, so validators see the complete request.
/// </summary>
internal static class RequestValidation
{
    /// <summary>Returns null when the request is valid (or has no validators), otherwise the field errors.</summary>
    public static async Task<IReadOnlyDictionary<string, string[]>?> ValidateAsync<TRequest>(TRequest request,
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var validators = services.GetServices<IValidator<TRequest>>().ToList();
        if (validators.Count == 0)
            return null;

        // One after the other: a validator may use the scoped DbContext, which doesn't allow parallel queries.
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
            failures.AddRange((await validator.ValidateAsync(context, cancellationToken)).Errors);

        if (failures.Count == 0)
            return null;

        return failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
    }
}

/// <summary>Builds the "validation failed" response of type <typeparamref name="TResult"/> (a <see cref="Result{T}"/>).
/// The factory method is looked up once per result type.</summary>
internal static class ValidationFailureResult<TResult>
{
    private static readonly Func<IReadOnlyDictionary<string, string[]>, TResult>? Factory = CreateFactory();

    public static TResult Create(IReadOnlyDictionary<string, string[]> errors) =>
        Factory is not null
            ? Factory(errors)
            : throw new InvalidOperationException(
                $"Request validators need a Result<T> response, but the handler returns {typeof(TResult).Name}.");

    private static Func<IReadOnlyDictionary<string, string[]>, TResult>? CreateFactory()
    {
        var type = typeof(TResult);
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Result<>))
            return null;

        return type.GetMethod(nameof(Result<object>.ValidationFailed), BindingFlags.Public | BindingFlags.Static)!
            .CreateDelegate<Func<IReadOnlyDictionary<string, string[]>, TResult>>();
    }
}
