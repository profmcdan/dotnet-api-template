namespace CleanArchTemplate.Infrastructure.Persistence.Outbox;

/// <summary>
/// One pending integration event, written in the same transaction as the state change that
/// produced it. This is what makes "the user was invited" and "the invite message was queued"
/// a single atomic fact instead of two things that can drift apart.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Assembly-qualified type name, used to deserialise for logging and replay.</summary>
    public required string Type { get; init; }

    /// <summary>Resolved topic including the environment prefix, frozen at write time.</summary>
    public required string Topic { get; init; }

    public required string PartitionKey { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Set once <see cref="Attempts"/> exhausts the budget; needs a human.</summary>
    public DateTimeOffset? DeadLetteredAt { get; set; }

    /// <summary>Backoff gate - the processor skips rows whose next attempt is in the future.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
}
