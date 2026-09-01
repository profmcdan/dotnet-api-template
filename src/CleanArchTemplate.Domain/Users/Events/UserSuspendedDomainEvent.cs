using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

public sealed record UserSuspendedDomainEvent(Guid UserId, string Email, string Reason) : DomainEvent;
