namespace CleanArchTemplate.Application.Abstractions.Notifications;

/// <summary>
/// At-least-once delivery from Kafka means the same email request can arrive twice. This turns
/// that into at-most-once sending by claiming an idempotency key before the send.
/// </summary>
public interface IEmailDeduplicator
{
    /// <summary>Returns false when the key was already claimed, i.e. the email has been sent.</summary>
    Task<bool> TryClaimAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Releases a claim so a failed send can be retried by the next delivery.</summary>
    Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
