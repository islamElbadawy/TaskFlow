using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.API.Common;

/// <summary>Turns handler results into HTTP responses: success → 200 with the <see cref="Result{T}"/>
/// (<c>{ isSuccess, data }</c>), failure → RFC 9457 ProblemDetails with the status code of its <see cref="ErrorType"/>.</summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess
            ? controller.Ok(result)
            : controller.Problem(result.ErrorType, result.ErrorMessage);

    /// <summary>ProblemDetails with <c>title</c> = the error type name and <c>detail</c> = the message.</summary>
    public static ObjectResult Problem(this ControllerBase controller, ErrorType errorType, string? detail) =>
        controller.Problem(
            statusCode: StatusCodeFor(errorType),
            title: errorType.ToString(),
            detail: detail);

    public static int StatusCodeFor(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}
