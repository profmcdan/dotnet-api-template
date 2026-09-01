using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CleanArchTemplate.Api.Middleware;

/// <summary>
/// Last line of defence. Unexpected exceptions become a ProblemDetails with the correlation id and
/// nothing else - stack traces and SQL text never reach the client, only the log.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // A cancelled request is the client hanging up, not a server fault.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, title, detail) = Classify(exception);

        ApiLog.UnhandledException(logger, exception, httpContext.Request.Path, httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = environment.IsDevelopment() ? exception.ToString() : detail,
            Instance = httpContext.Request.Path,
        };

        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static (int Status, string Title, string Detail) Classify(Exception exception) => exception switch
    {
        // 23505 = unique_violation: a race lost the insert, which is a conflict, not a server bug.
        PostgresException { SqlState: "23505" } =>
            (StatusCodes.Status409Conflict, "Conflict", "The record already exists."),

        PostgresException { SqlState: "23503" } =>
            (StatusCodes.Status409Conflict, "Conflict", "A related record is missing or still in use."),

        TimeoutException or NpgsqlException =>
            (StatusCodes.Status503ServiceUnavailable, "Service unavailable", "A downstream dependency did not respond in time."),

        BadHttpRequestException =>
            (StatusCodes.Status400BadRequest, "Bad request", "The request could not be read."),

        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "An unexpected error occurred. Quote the correlation id when contacting support."),
    };
}

internal static partial class ApiLog
{
    [LoggerMessage(EventId = 6000, Level = LogLevel.Error, Message = "Unhandled exception for {Path} (correlation {CorrelationId})")]
    public static partial void UnhandledException(ILogger logger, Exception exception, string path, string correlationId);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "{ApplicationName} starting in {Environment}")]
    public static partial void Starting(ILogger logger, string applicationName, string environment);
}
