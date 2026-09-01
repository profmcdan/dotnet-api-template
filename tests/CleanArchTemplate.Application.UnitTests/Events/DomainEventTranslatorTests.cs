using CleanArchTemplate.Application.Events;
using CleanArchTemplate.Contracts.Users;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users.Events;

namespace CleanArchTemplate.Application.UnitTests.Events;

public sealed class DomainEventTranslatorTests
{
    private readonly DomainEventTranslator _translator = new(new TestLinkBuilder());

    [Fact]
    public void Builds_the_accept_url_from_configuration_not_from_the_request()
    {
        var userId = Guid.CreateVersion7();
        var domainEvent = new UserInvitedDomainEvent(
            userId, "invitee@example.com", "Ada", Guid.CreateVersion7(), "raw-token",
            DateTimeOffset.UtcNow.AddDays(7), Guid.CreateVersion7(), "Root Admin");

        var translated = _translator.Translate(domainEvent).ShouldHaveSingleItem();
        var integration = translated.Event.ShouldBeOfType<UserInvitedIntegrationEvent>();

        integration.AcceptUrl.ShouldBe("https://app.test/accept-invitation?token=raw-token");
        integration.IsResend.ShouldBeFalse();
        translated.PartitionKey.ShouldBe(userId.ToString());
    }

    [Fact]
    public void Preserves_the_event_id_so_consumers_can_deduplicate()
    {
        var domainEvent = new UserActivatedDomainEvent(Guid.CreateVersion7(), "a@b.com", "Ada");

        var translated = _translator.Translate(domainEvent).ShouldHaveSingleItem();

        translated.Event.EventId.ShouldBe(domainEvent.EventId);
        translated.Event.OccurredAt.ShouldBe(domainEvent.OccurredAt);
    }

    [Fact]
    public void Marks_a_resend_so_the_email_can_read_differently()
    {
        var domainEvent = new UserInvitationResentDomainEvent(
            Guid.CreateVersion7(), "invitee@example.com", "Ada", Guid.CreateVersion7(), "raw",
            DateTimeOffset.UtcNow.AddDays(7), Guid.CreateVersion7(), "Root Admin");

        var integration = _translator.Translate(domainEvent).ShouldHaveSingleItem()
            .Event.ShouldBeOfType<UserInvitedIntegrationEvent>();

        integration.IsResend.ShouldBeTrue();
    }

    [Fact]
    public void Unmapped_domain_events_stay_internal() =>
        _translator.Translate(new UnpublishedEvent()).ShouldBeEmpty();

    private sealed record UnpublishedEvent : DomainEvent;
}
