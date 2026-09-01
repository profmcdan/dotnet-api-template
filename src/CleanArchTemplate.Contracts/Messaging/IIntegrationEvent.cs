namespace CleanArchTemplate.Contracts.Messaging;

/// <summary>
/// The wire contract between services. Integration events are versioned and additive-only:
/// never remove or repurpose a member, add a new event type instead.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Stable idempotency key. Consumers must tolerate seeing the same id twice.</summary>
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    /// <summary>Correlates every message produced while handling one inbound request.</summary>
    string? CorrelationId { get; }
}
