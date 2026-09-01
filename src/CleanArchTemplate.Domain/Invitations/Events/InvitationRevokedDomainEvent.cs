using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Invitations.Events;

public sealed record InvitationRevokedDomainEvent(
    Guid InvitationId,
    Guid UserId,
    string Email,
    Guid RevokedByUserId) : DomainEvent;
