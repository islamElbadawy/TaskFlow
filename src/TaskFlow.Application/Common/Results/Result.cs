using System.Text.Json.Serialization;

namespace TaskFlow.Application.Common.Results;

/// <summary>
/// Why a <see cref="Result{T}"/> failed. The API maps it to an HTTP status code
/// (Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, Failure 500).
/// </summary>
public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Failure
}

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    /// <summary>Not serialized: failures are returned as ProblemDetails, successes as <c>{ isSuccess, data }</c>.</summary>
    [JsonIgnore]
    public ErrorType ErrorType { get; private set; }

    /// <summary>Field errors from the request validators (property name → messages); null for every other failure.
    /// The API returns them as the <c>errors</c> object of a 400 ValidationProblemDetails.</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; private set; }

    public static Result<T> Success(T data)
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data,
        };
    }

    public static Result<T> Failure(string errorMessage, ErrorType errorType = ErrorType.Failure)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Errors = new List<string> { errorMessage },
            ErrorType = errorType
        };
    }

    public static Result<T> Failure(List<string> errors, ErrorType errorType = ErrorType.Failure)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorMessage = string.Join(',', errors),
            Errors = errors,
            ErrorType = errorType
        };
    }

    /// <summary>The input is invalid (400).</summary>
    public static Result<T> Validation(string message) => Failure(message, ErrorType.Validation);

    /// <summary>The request validators rejected the input (400 with the field errors). Created by the dispatchers.</summary>
    public static Result<T> ValidationFailed(IReadOnlyDictionary<string, string[]> errors) => new()
    {
        IsSuccess = false,
        ErrorMessage = "One or more validation errors occurred.",
        Errors = errors.Values.SelectMany(messages => messages).ToList(),
        ErrorType = ErrorType.Validation,
        ValidationErrors = errors
    };

    /// <summary>The requested resource does not exist (404).</summary>
    public static Result<T> NotFound(string message) => Failure(message, ErrorType.NotFound);

    /// <summary>The request conflicts with the current state, e.g. a duplicate (409).</summary>
    public static Result<T> Conflict(string message) => Failure(message, ErrorType.Conflict);

    /// <summary>The caller could not be authenticated (401).</summary>
    public static Result<T> Unauthorized(string message) => Failure(message, ErrorType.Unauthorized);

    /// <summary>The caller is authenticated but not allowed to do this (403).</summary>
    public static Result<T> Forbidden(string message) => Failure(message, ErrorType.Forbidden);
}
