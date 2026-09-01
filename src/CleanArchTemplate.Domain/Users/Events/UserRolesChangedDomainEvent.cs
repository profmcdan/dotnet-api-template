using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

public sealed record UserRolesChangedDomainEvent(
    Guid UserId,
    string Email,
    IReadOnlyCollection<string> Roles) : DomainEvent;
