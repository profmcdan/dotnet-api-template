using System.Text;
using System.Text.Json;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Messaging;

/// <summary>
/// Base class for a single-topic consumer loop with manual offset commits, bounded in-process
/// retries and a dead-letter fallback.
/// <para>
/// Offsets are stored only after <see cref="HandleAsync"/> returns, so a crash mid-handle replays
/// the message: delivery is at-least-once and handlers must be idempotent.
/// </para>
/// </summary>
public abstract class KafkaConsumerBase<TMessage>(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger logger) : BackgroundService
    where TMessage : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected KafkaOptions KafkaOptions { get; } = kafkaOptions.Value;

    protected ITopicResolver TopicResolver { get; } = topics;

    protected ILogger Logger { get; } = logger;

    /// <summary>Logical topic (unprefixed) this consumer subscribes to.</summary>
    protected abstract string LogicalTopic { get; }

    /// <summary>
    /// Consumer group suffix. Each independent consumer needs its own group, otherwise two
    /// consumers in one group split the partitions and each sees only part of the traffic.
    /// </summary>
    protected abstract string GroupSuffix { get; }

    /// <summary>Where messages go once retries are exhausted. Null discards them after logging.</summary>
    protected virtual string? DeadLetterTopic => null;

    protected abstract Task HandleAsync(TMessage message, IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hand control back so host startup is not blocked by broker connection setup.
        await Task.Yield();

        var topic = TopicResolver.Resolve(LogicalTopic);
        var groupId = $"{KafkaOptions.ConsumerGroupId}.{GroupSuffix}";

        using var consumer = new ConsumerBuilder<string, string>(KafkaClientFactory.CreateConsumerConfig(KafkaOptions, groupId))
            .SetErrorHandler((_, error) => KafkaLog.ConsumerError(Logger, topic, error.Reason, error.IsFatal))
            .Build();

        consumer.Subscribe(topic);
        KafkaLog.ConsumerStarted(Logger, GetType().Name, topic, groupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;

                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    KafkaLog.ConsumerError(Logger, topic, ex.Error.Reason, ex.Error.IsFatal);
                    continue;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                await ProcessWithRetriesAsync(consumer, result, topic, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            // Leave the group cleanly so rebalancing does not wait for the session to time out.
            consumer.Close();
        }
    }

    private async Task ProcessWithRetriesAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        string topic,
        CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= KafkaOptions.MaxDeliveryAttempts; attempt++)
        {
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value, SerializerOptions)
                    ?? throw new JsonException($"Message on {topic} deserialised to null.");

                await using var scope = scopeFactory.CreateAsyncScope();
                await HandleAsync(message, scope.ServiceProvider, stoppingToken);

                consumer.StoreOffset(result);
                consumer.Commit(result);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < KafkaOptions.MaxDeliveryAttempts && ex is not JsonException)
            {
                KafkaLog.MessageRetry(Logger, topic, result.Offset.Value, attempt);

                // Exponential backoff, capped so a poison message cannot stall the partition for minutes.
                var delay = TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, attempt - 1), 10_000));
                await Task.Delay(delay, stoppingToken);
            }
            catch (Exception ex)
            {
                KafkaLog.MessageDeadLettered(Logger, ex, topic, result.Offset.Value);
                await DeadLetterAsync(result, ex, stoppingToken);

                // Commit regardless: the message is parked, and not committing would replay it forever.
                consumer.StoreOffset(result);
                consumer.Commit(result);
                return;
            }
        }
    }

    private async Task DeadLetterAsync(ConsumeResult<string, string> result, Exception exception, CancellationToken cancellationToken)
    {
        if (DeadLetterTopic is not { } logicalDeadLetter)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dlq-reason"] = exception.GetType().Name,
                ["dlq-message"] = Truncate(exception.Message, 512),
                ["dlq-source-topic"] = result.Topic,
                ["dlq-source-offset"] = result.Offset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            await publisher.PublishAsync(
                TopicResolver.Resolve(logicalDeadLetter),
                result.Message.Key ?? Guid.CreateVersion7().ToString(),
                result.Message.Value,
                headers,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // The dead-letter path failing must not take the consumer down with it.
            KafkaLog.ConsumerError(Logger, logicalDeadLetter, ex.Message, isFatal: false);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    protected static string? HeaderValue(ConsumeResult<string, string> result, string key) =>
        result.Message.Headers.TryGetLastBytes(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
}
