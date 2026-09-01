using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users.Events;

public sealed record UserReinstatedDomainEvent(Guid UserId, string Email) : DomainEvent;
