namespace CleanArchTemplate.Domain.Common;

/// <summary>
/// Non-generic view of an aggregate's pending events, so infrastructure can collect them from a
/// change tracker without knowing the closed generic <c>AggregateRoot&lt;TId&gt;</c>.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
