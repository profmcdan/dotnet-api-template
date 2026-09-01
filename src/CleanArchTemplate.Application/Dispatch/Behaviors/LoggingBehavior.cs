using System.Diagnostics;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Application.Dispatch.Behaviors;

/// <summary>
/// Logs one line per request with its outcome and duration. Only the request type name and the
/// error code are logged - never the request body, which routinely holds passwords and tokens.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var timestamp = Stopwatch.GetTimestamp();

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["RequestType"] = RequestName });

        try
        {
            var response = await next();
            var elapsedMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            if (response.IsSuccess)
            {
                PipelineLog.RequestCompleted(logger, RequestName, elapsedMs);
            }
            else
            {
                PipelineLog.RequestFailed(logger, RequestName, elapsedMs, response.Error.Type, response.Error.Code);
            }

            return response;
        }
        catch (Exception ex)
        {
            PipelineLog.RequestFaulted(logger, ex, RequestName, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            throw;
        }
    }
}
