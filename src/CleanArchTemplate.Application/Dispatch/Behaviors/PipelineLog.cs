using CleanArchTemplate.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Application.Dispatch.Behaviors;

/// <summary>
/// Source-generated log methods for the request pipeline. Using the generator instead of the
/// <c>ILogger.LogX</c> extensions keeps the hot path allocation-free when the level is disabled.
/// </summary>
internal static partial class PipelineLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "{RequestName} completed in {ElapsedMs:F1}ms")]
    public static partial void RequestCompleted(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "{RequestName} failed in {ElapsedMs:F1}ms with {ErrorType} {ErrorCode}")]
    public static partial void RequestFailed(ILogger logger, string requestName, double elapsedMs, ErrorType errorType, string errorCode);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "{RequestName} threw after {ElapsedMs:F1}ms")]
    public static partial void RequestFaulted(ILogger logger, Exception exception, string requestName, double elapsedMs);
}
