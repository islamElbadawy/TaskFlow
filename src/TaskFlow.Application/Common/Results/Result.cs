namespace TaskFlow.Application.Common.Results;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public List<string> Errors { get; private set; } = new();

    public static Result<T> Success(T data)
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data,
        };
    }

    public static Result<T> Failure(string errorMessages)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessages,
            Errors = new List<string> { errorMessages }
        };
    }

    public static Result<T> Failure(List<string> errors)
    {
        return new Result<T>
        {
            IsSuccess = false,
            ErrorMessage = string.Join(',', errors),
            Errors = errors
        };
    }
}