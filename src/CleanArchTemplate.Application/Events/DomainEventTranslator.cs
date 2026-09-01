using CleanArchTemplate.Application.Abstractions.Links;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Contracts.Users;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations.Events;
using CleanArchTemplate.Domain.Users.Events;

namespace CleanArchTemplate.Application.Events;

/// <summary>
/// The one place internal domain events become the public integration contract. Putting it here
/// keeps the domain free of any knowledge of Kafka, URLs or email, and makes the full set of
/// published events reviewable in a single file.
/// </summary>
internal sealed class DomainEventTranslator(IAppLinkBuilder links) : IDomainEventTranslator
{
    public IReadOnlyCollection<TranslatedEvent> Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        UserInvitedDomainEvent e =>
        [
            new TranslatedEvent(
                new UserInvitedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                    FullName = e.FullName,
                    InvitationId = e.InvitationId,
                    AcceptUrl = links.AcceptInvitationUrl(e.InvitationToken),
                    ExpiresAt = e.ExpiresAt,
                    InvitedByName = e.InvitedByName,
                    IsResend = false,
                },
                e.UserId.ToString()),
        ],

        UserInvitationResentDomainEvent e =>
        [
            new TranslatedEvent(
                new UserInvitedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                    FullName = e.FullName,
                    InvitationId = e.InvitationId,
                    AcceptUrl = links.AcceptInvitationUrl(e.InvitationToken),
                    ExpiresAt = e.ExpiresAt,
                    InvitedByName = e.ResentByName,
                    IsResend = true,
                },
                e.UserId.ToString()),
        ],

        UserActivatedDomainEvent e =>
        [
            new TranslatedEvent(
                new UserActivatedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                    FullName = e.FullName,
                },
                e.UserId.ToString()),
        ],

        UserSuspendedDomainEvent e =>
        [
            new TranslatedEvent(
                new UserSuspendedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                    Reason = e.Reason,
                },
                e.UserId.ToString()),
        ],

        UserReinstatedDomainEvent e =>
        [
            new TranslatedEvent(
                new UserReinstatedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                },
                e.UserId.ToString()),
        ],

        UserRolesChangedDomainEvent e =>
        [
            new TranslatedEvent(
                new UserRolesChangedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    UserId = e.UserId,
                    Email = e.Email,
                    Roles = e.Roles,
                },
                e.UserId.ToString()),
        ],

        InvitationRevokedDomainEvent e =>
        [
            new TranslatedEvent(
                new InvitationRevokedIntegrationEvent
                {
                    EventId = e.EventId,
                    OccurredAt = e.OccurredAt,
                    InvitationId = e.InvitationId,
                    UserId = e.UserId,
                    Email = e.Email,
                },
                e.UserId.ToString()),
        ],

        // Not every domain event is public. Anything unmapped stays internal by design.
        _ => [],
    };
}
