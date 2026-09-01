using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

public sealed record UserActivatedDomainEvent(
    Guid UserId,
    string Email,
    string FullName) : DomainEvent;
