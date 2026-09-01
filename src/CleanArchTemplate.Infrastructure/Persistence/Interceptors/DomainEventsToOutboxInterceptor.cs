using System.Text.Json;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CleanArchTemplate.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Drains domain events off tracked aggregates and writes them to the outbox inside the same
/// <c>SaveChanges</c>. Publishing to Kafka from a handler would risk a message describing a
/// transaction that later rolled back; this cannot.
/// </summary>
internal sealed class DomainEventsToOutboxInterceptor(
    IDomainEventTranslator translator,
    ITopicResolver topics,
    IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        WriteOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void WriteOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToArray();

        if (aggregates.Length == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var messages = new List<OutboxMessage>();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                foreach (var translated in translator.Translate(domainEvent))
                {
                    var eventType = translated.Event.GetType();

                    messages.Add(new OutboxMessage
                    {
                        Id = translated.Event.EventId,
                        Type = eventType.FullName ?? eventType.Name,
                        Topic = topics.ResolveFor(eventType),
                        PartitionKey = translated.PartitionKey,
                        Payload = JsonSerializer.Serialize(translated.Event, eventType, SerializerOptions),
                        CorrelationId = translated.Event.CorrelationId,
                        OccurredAt = translated.Event.OccurredAt,
                        NextAttemptAt = now,
                    });
                }
            }

            // Clear only after translation, so a translator throw leaves the events in place.
            aggregate.ClearDomainEvents();
        }

        if (messages.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(messages);
        }
    }
}
