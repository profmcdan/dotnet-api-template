namespace CleanArchTemplate.Domain.Common;

/// <summary>
/// A fact that has already happened inside the domain. Raised by aggregates,
/// collected by the unit of work and dispatched after the transaction commits.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
