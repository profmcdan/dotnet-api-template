using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Worker;

internal static partial class WorkerLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Email {IdempotencyKey} already sent; skipping duplicate delivery")]
    public static partial void DuplicateEmailSkipped(ILogger logger, string idempotencyKey);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Purged {Count} expired refresh token(s)")]
    public static partial void TokensPurged(ILogger logger, int count);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "Scheduled cleanup failed; will retry on the next interval")]
    public static partial void CleanupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Warning, Message = "Kafka topics are missing: {Topics}. Run the migrator to create them.")]
    public static partial void MissingTopics(ILogger logger, string topics);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Information, Message = "Worker host started with {ConsumerCount} consumer(s)")]
    public static partial void Started(ILogger logger, int consumerCount);
}
