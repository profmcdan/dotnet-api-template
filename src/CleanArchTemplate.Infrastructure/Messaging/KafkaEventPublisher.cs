using System.Text;
using System.Text.Json;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Messaging;

/// <summary>
/// Singleton producer. One <see cref="IProducer{TKey,TValue}"/> is shared process-wide because
/// the client is thread-safe and holds the broker connections and batching buffers.
/// </summary>
internal sealed class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly ITopicResolver _topics;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ITopicResolver topics, ILogger<KafkaEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _topics = topics;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(KafkaClientFactory.CreateProducerConfig(options.Value))
            .SetErrorHandler((_, error) => KafkaLog.ProducerError(logger, error.Reason, error.IsFatal))
            .Build();
    }

    public Task PublishAsync<TEvent>(TEvent integrationEvent, string partitionKey, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var payload = JsonSerializer.Serialize(integrationEvent, eventType, SerializerOptions);

        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["event-type"] = eventType.FullName ?? eventType.Name,
            ["event-id"] = integrationEvent.EventId.ToString(),
        };

        if (integrationEvent.CorrelationId is { } correlationId)
        {
            headers["correlation-id"] = correlationId;
        }

        return PublishAsync(_topics.ResolveFor(eventType), partitionKey, payload, headers, cancellationToken);
    }

    public async Task PublishAsync(
        string topic,
        string partitionKey,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string> { Key = partitionKey, Value = payload };

        if (headers is { Count: > 0 })
        {
            message.Headers = [];
            foreach (var (key, value) in headers)
            {
                message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
            }
        }

        var result = await _producer.ProduceAsync(topic, message, cancellationToken);
        KafkaLog.Produced(_logger, topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        // Drain in-flight batches; without this a fast-exiting worker can drop acknowledged writes.
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
