using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

public sealed record UserInvitationResentDomainEvent(
    Guid UserId,
    string Email,
    string FullName,
    Guid InvitationId,
    string InvitationToken,
    DateTimeOffset ExpiresAt,
    Guid ResentByUserId,
    string ResentByName) : DomainEvent;
