using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Application.Abstractions.Messaging;

/// <summary>
/// Maps internal domain events onto the public integration contract. Deliberately many-to-many:
/// some domain events publish nothing, others fan out to several topics.
/// </summary>
public interface IDomainEventTranslator
{
    IReadOnlyCollection<TranslatedEvent> Translate(IDomainEvent domainEvent);
}

/// <summary>An integration event plus the partition key that preserves its ordering.</summary>
public sealed record TranslatedEvent(IIntegrationEvent Event, string PartitionKey);
