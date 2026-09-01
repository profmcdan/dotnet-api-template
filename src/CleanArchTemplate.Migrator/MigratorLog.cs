using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Migrator;

internal static partial class MigratorLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Running step {StepName}")]
    public static partial void StepStarted(ILogger logger, string stepName);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Step {StepName} finished in {ElapsedMs:F0}ms")]
    public static partial void StepFinished(ILogger logger, string stepName, double elapsedMs);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Error, Message = "Step {StepName} failed")]
    public static partial void StepFailed(ILogger logger, Exception exception, string stepName);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "No pending database migrations")]
    public static partial void NoPendingMigrations(ILogger logger);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Information, Message = "Applying {Count} migration(s): {Migrations}")]
    public static partial void ApplyingMigrations(ILogger logger, int count, string migrations);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information, Message = "Applied {Count} migration(s)")]
    public static partial void MigrationsApplied(ILogger logger, int count);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Information, Message = "Seeding Kafka topics with prefix {Prefix}")]
    public static partial void SeedingTopics(ILogger logger, string prefix);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Information, Message = "All Kafka topics already exist")]
    public static partial void TopicsUpToDate(ILogger logger);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Information, Message = "Created {Count} Kafka topic(s): {Topics}")]
    public static partial void TopicsCreated(ILogger logger, int count, string topics);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Information, Message = "Administrator seed skipped: {Reason}")]
    public static partial void SeedSkipped(ILogger logger, string reason);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Information, Message = "Seeded bootstrap administrator {Email}")]
    public static partial void SeededAdministrator(ILogger logger, string email);

    [LoggerMessage(EventId = 7011, Level = LogLevel.Information, Message = "Waiting for {Dependency} to become available")]
    public static partial void WaitingForDependency(ILogger logger, string dependency);

    [LoggerMessage(EventId = 7012, Level = LogLevel.Information, Message = "Migrator completed successfully")]
    public static partial void Completed(ILogger logger);
}
