using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Persistence.Outbox;

/// <summary>
/// Drains the outbox onto Kafka.
/// <para>
/// Rows are claimed with <c>FOR UPDATE SKIP LOCKED</c>, so several replicas can run this loop
/// concurrently and each takes a disjoint batch. Publishing is at-least-once: the broker may
/// acknowledge a message the instant before the row is marked processed, which is exactly why
/// every integration event carries a stable <c>EventId</c> for consumers to deduplicate on.
/// </para>
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(_options.PollInterval);
        var cleanupDue = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishPendingAsync(stoppingToken);

                if (published > 0)
                {
                    OutboxLog.BatchPublished(logger, published);

                    // A full batch means more work is waiting; skip the tick and keep draining.
                    if (published >= _options.BatchSize)
                    {
                        continue;
                    }
                }

                if (DateTimeOffset.UtcNow >= cleanupDue)
                {
                    var removed = await CleanupAsync(stoppingToken);
                    if (removed > 0)
                    {
                        OutboxLog.Cleaned(logger, removed);
                    }

                    cleanupDue = DateTimeOffset.UtcNow.AddHours(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad tick kill the loop; the next one retries.
                OutboxLog.LoopFailed(logger, ex);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        // Retries are enabled on the connection, so the transaction must run inside the execution
        // strategy - otherwise EF refuses to start a user transaction at all.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            // SKIP LOCKED is what makes horizontal scaling of this worker safe.
            var messages = await context.OutboxMessages
                .FromSql($"""
                    SELECT * FROM outbox_messages
                    WHERE processed_at IS NULL
                      AND dead_lettered_at IS NULL
                      AND (next_attempt_at IS NULL OR next_attempt_at <= {now})
                    ORDER BY occurred_at
                    LIMIT {_options.BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            var published = 0;

            foreach (var message in messages)
            {
                try
                {
                    var headers = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["event-type"] = message.Type,
                        ["event-id"] = message.Id.ToString(),
                    };

                    if (message.CorrelationId is { } correlationId)
                    {
                        headers["correlation-id"] = correlationId;
                    }

                    await publisher.PublishAsync(message.Topic, message.PartitionKey, message.Payload, headers, cancellationToken);

                    message.ProcessedAt = clock.UtcNow;
                    message.LastError = null;
                    published++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Attempts++;
                    message.LastError = Truncate(ex.Message, 2000);

                    if (message.Attempts >= _options.MaxAttempts)
                    {
                        message.DeadLetteredAt = clock.UtcNow;
                        OutboxLog.DeadLettered(logger, ex, message.Id, message.Topic, message.Attempts);
                    }
                    else
                    {
                        // Exponential backoff capped at five minutes.
                        var backoff = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, message.Attempts), 300));
                        message.NextAttemptAt = clock.UtcNow.Add(backoff);
                        OutboxLog.PublishFailed(logger, ex, message.Id, message.Topic, message.Attempts);
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return published;
        });
    }

    private async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var cutoff = clock.UtcNow.AddDays(-_options.RetentionDays);

        // Dead letters are deliberately kept: they are the audit trail of what never got out.
        return await context.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

internal static partial class OutboxLog
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Debug, Message = "Published {Count} outbox message(s)")]
    public static partial void BatchPublished(ILogger logger, int count);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "Outbox message {MessageId} to {Topic} failed (attempt {Attempts}); will retry")]
    public static partial void PublishFailed(ILogger logger, Exception exception, Guid messageId, string topic, int attempts);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Outbox message {MessageId} to {Topic} dead-lettered after {Attempts} attempts")]
    public static partial void DeadLettered(ILogger logger, Exception exception, Guid messageId, string topic, int attempts);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Outbox processing loop failed; retrying on the next tick")]
    public static partial void LoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "Removed {Count} processed outbox message(s)")]
    public static partial void Cleaned(ILogger logger, int count);
}
