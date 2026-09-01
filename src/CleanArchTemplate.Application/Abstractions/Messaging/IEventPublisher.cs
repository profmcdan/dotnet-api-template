using CleanArchTemplate.Contracts.Messaging;

namespace CleanArchTemplate.Application.Abstractions.Messaging;

/// <summary>
/// Publishes directly to the broker. Only the outbox processor and standalone workers should
/// use this - request handlers enqueue through the outbox instead, so the message and the
/// state change it describes commit together.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, string partitionKey, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    Task PublishAsync(string topic, string partitionKey, string payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);
}
