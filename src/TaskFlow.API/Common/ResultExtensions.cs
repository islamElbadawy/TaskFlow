using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.API.Common;

/// <summary>Turns handler results into HTTP responses: success → 200 with the <see cref="Result{T}"/>
/// (<c>{ isSuccess, data }</c>), failure → RFC 9457 ProblemDetails with the status code of its <see cref="ErrorType"/>.
/// Validator failures become a 400 ValidationProblemDetails with an <c>errors</c> object per field.</summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
            return controller.Ok(result);

        if (result.ValidationErrors is { } errors)
        {
            var modelState = new ModelStateDictionary();
            foreach (var (field, messages) in errors)
                foreach (var message in messages)
                    modelState.AddModelError(field, message);

            return controller.ValidationProblem(detail: result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest,
                title: nameof(ErrorType.Validation), modelStateDictionary: modelState);
        }

        return controller.Problem(result.ErrorType, result.ErrorMessage);
    }

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
