using CleanArchTemplate.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchTemplate.Api.Extensions;

/// <summary>
/// The single translation from a domain <see cref="Result"/> to an HTTP response. Every controller
/// funnels through here, so status codes stay consistent and no handler ever picks one itself.
/// </summary>
public static class ResultExtensions
{
    private const string ErrorCodeExtension = "errorCode";

    public static IActionResult ToActionResult(this Result result, ControllerBase controller) =>
        result.IsSuccess ? controller.NoContent() : Problem(result.Error, controller);

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
        result.IsSuccess ? controller.Ok(result.Value) : Problem(result.Error, controller);

    public static IActionResult ToCreatedResult<T>(this Result<T> result, ControllerBase controller, string actionName, object routeValues) =>
        result.IsSuccess
            ? controller.CreatedAtAction(actionName, routeValues, result.Value)
            : Problem(result.Error, controller);

    private static IActionResult Problem(Error error, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(controller);

        if (error is ValidationError validation)
        {
            var problem = new ValidationProblemDetails(validation.Failures.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#name-400-bad-request",
            };

            problem.Extensions[ErrorCodeExtension] = validation.Code;
            return controller.BadRequest(problem);
        }

        var status = StatusCodeFor(error.Type);

        var details = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(error.Type),
            Detail = error.Description,
            Type = $"https://datatracker.ietf.org/doc/html/rfc9110#name-{status}",
        };

        details.Extensions[ErrorCodeExtension] = error.Code;

        return controller.StatusCode(status, details);
    }

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Bad request",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unavailable => "Service unavailable",
        _ => "An unexpected error occurred",
    };
}
