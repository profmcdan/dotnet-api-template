using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

/// <summary>
/// Raised when an administrator invites a new user. Carries the single-use invitation
/// token because the outbox row is the only thing that can build the accept link, and the
/// aggregate deliberately stores nothing but its hash.
/// </summary>
public sealed record UserInvitedDomainEvent(
    Guid UserId,
    string Email,
    string FullName,
    Guid InvitationId,
    string InvitationToken,
    DateTimeOffset ExpiresAt,
    Guid InvitedByUserId,
    string InvitedByName) : DomainEvent;
