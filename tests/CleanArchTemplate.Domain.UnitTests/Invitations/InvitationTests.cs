using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Domain.UnitTests.Invitations;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    [Fact]
    public void Issue_stores_only_the_token_hash()
    {
        var issued = Issue();

        issued.Invitation.TokenHash.ShouldBe(InvitationToken.HashOf(issued.Token.Raw));
        issued.Invitation.TokenHash.ShouldNotBe(issued.Token.Raw);
    }

    [Fact]
    public void Issued_tokens_are_unique()
    {
        var first = InvitationToken.Issue();
        var second = InvitationToken.Issue();

        first.Raw.ShouldNotBe(second.Raw);
        first.Hash.ShouldNotBe(second.Hash);
    }

    [Fact]
    public void Accept_marks_the_invitation_used()
    {
        var invitation = Issue().Invitation;

        invitation.Accept(Now).IsSuccess.ShouldBeTrue();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.AcceptedAt.ShouldBe(Now);
    }

    [Fact]
    public void Accept_cannot_be_replayed()
    {
        var invitation = Issue().Invitation;
        invitation.Accept(Now);

        invitation.Accept(Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Accept_is_rejected_after_expiry()
    {
        var invitation = Issue().Invitation;

        var result = invitation.Accept(Now.Add(Lifetime).AddSeconds(1));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InvitationErrors.Expired);
    }

    [Fact]
    public void Accept_is_rejected_once_revoked()
    {
        var invitation = Issue().Invitation;
        invitation.Revoke(Guid.CreateVersion7(), Now);

        invitation.Accept(Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Revoke_only_applies_to_a_pending_invitation()
    {
        var invitation = Issue().Invitation;
        invitation.Accept(Now);

        invitation.Revoke(Guid.CreateVersion7(), Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Resend_rotates_the_token_so_the_previous_link_stops_working()
    {
        var issued = Issue();
        var originalHash = issued.Invitation.TokenHash;
        var later = Now.Add(Invitation.MinimumResendInterval).AddSeconds(1);

        var result = UserInvitationService.Resend(issued.User, issued.Invitation, Inviter(), Lifetime, later);

        result.IsSuccess.ShouldBeTrue();
        issued.Invitation.TokenHash.ShouldNotBe(originalHash);
        issued.Invitation.TokenHash.ShouldBe(InvitationToken.HashOf(result.Value.Raw));
        issued.Invitation.SendCount.ShouldBe(2);
    }

    [Fact]
    public void Resend_is_throttled_to_stop_the_endpoint_being_used_as_a_mail_amplifier()
    {
        var issued = Issue();

        var result = UserInvitationService.Resend(issued.User, issued.Invitation, Inviter(), Lifetime, Now.AddSeconds(5));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InvitationErrors.ResendTooSoon);
    }

    [Fact]
    public void Resend_is_rejected_once_the_user_has_activated()
    {
        var issued = Issue();
        issued.User.Activate("hashed", Now);
        var later = Now.Add(Invitation.MinimumResendInterval).AddSeconds(1);

        UserInvitationService.Resend(issued.User, issued.Invitation, Inviter(), Lifetime, later)
            .IsFailure.ShouldBeTrue();
    }

    private static IssuedInvitation Issue() =>
        UserInvitationService.Invite(
            Email.Create("invitee@example.com").Value,
            "Ada Lovelace",
            [UserRoles.Member],
            Inviter(),
            Lifetime,
            Now).Value;

    private static User Inviter() =>
        User.CreateActive(Email.Create("admin@example.com").Value, "Root Admin", "hash", [UserRoles.Administrator], Now).Value;
}
