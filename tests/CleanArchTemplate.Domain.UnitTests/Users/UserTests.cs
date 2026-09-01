using CleanArchTemplate.Domain.Users;
using CleanArchTemplate.Domain.Users.Events;

namespace CleanArchTemplate.Domain.UnitTests.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static Email AnEmail(string value = "invitee@example.com") => Email.Create(value).Value;

    [Fact]
    public void Invite_creates_a_user_that_cannot_yet_authenticate()
    {
        var user = User.Invite(AnEmail(), "Ada Lovelace", [UserRoles.Member]).Value;

        user.Status.ShouldBe(UserStatus.Invited);
        user.PasswordHash.ShouldBeNull();
        user.CanAuthenticate.ShouldBeFalse();
    }

    [Fact]
    public void Invite_rejects_an_empty_name() =>
        User.Invite(AnEmail(), "  ", [UserRoles.Member]).IsFailure.ShouldBeTrue();

    [Fact]
    public void Invite_rejects_an_unknown_role() =>
        User.Invite(AnEmail(), "Ada", ["superuser"]).IsFailure.ShouldBeTrue();

    [Fact]
    public void Invite_requires_at_least_one_role() =>
        User.Invite(AnEmail(), "Ada", []).IsFailure.ShouldBeTrue();

    [Fact]
    public void Invite_normalises_and_deduplicates_roles()
    {
        var user = User.Invite(AnEmail(), "Ada", ["Member", "member", "MANAGER"]).Value;

        user.Roles.ShouldBe([UserRoles.Member, UserRoles.Manager]);
    }

    [Fact]
    public void Activate_sets_credentials_and_raises_the_activation_event()
    {
        var user = User.Invite(AnEmail(), "Ada", [UserRoles.Member]).Value;
        user.ClearDomainEvents();

        var result = user.Activate("hashed", Now);

        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate.ShouldBeTrue();
        user.PasswordChangedAt.ShouldBe(Now);
        user.DomainEvents.OfType<UserActivatedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Activate_is_rejected_once_the_user_is_already_active()
    {
        var user = User.Invite(AnEmail(), "Ada", [UserRoles.Member]).Value;
        user.Activate("hashed", Now);

        user.Activate("other", Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Activating_bumps_the_security_stamp_so_older_tokens_stop_validating()
    {
        var user = User.Invite(AnEmail(), "Ada", [UserRoles.Member]).Value;
        var before = user.SecurityStamp;

        user.Activate("hashed", Now);

        user.SecurityStamp.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void Suspend_blocks_authentication_and_records_the_reason()
    {
        var user = ActiveUser();

        var result = user.Suspend("Policy violation");

        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Suspended);
        user.CanAuthenticate.ShouldBeFalse();
        user.SuspensionReason.ShouldBe("Policy violation");
        user.DomainEvents.OfType<UserSuspendedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Suspend_twice_is_a_conflict()
    {
        var user = ActiveUser();
        user.Suspend("First");

        user.Suspend("Second").IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Reinstate_returns_an_active_user_to_active()
    {
        var user = ActiveUser();
        user.Suspend("Mistake");

        user.Reinstate().IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.SuspensionReason.ShouldBeNull();
    }

    [Fact]
    public void Reinstate_returns_a_never_activated_user_to_invited()
    {
        var user = User.Invite(AnEmail(), "Ada", [UserRoles.Member]).Value;
        user.Suspend("Held");

        user.Reinstate();

        user.Status.ShouldBe(UserStatus.Invited);
    }

    [Fact]
    public void Reinstate_is_rejected_when_the_user_is_not_suspended() =>
        ActiveUser().Reinstate().IsFailure.ShouldBeTrue();

    [Fact]
    public void ChangeRoles_raises_an_event_only_when_the_set_actually_changes()
    {
        var user = ActiveUser();
        user.ClearDomainEvents();

        user.ChangeRoles([UserRoles.Member]);
        user.DomainEvents.ShouldBeEmpty();

        user.ChangeRoles([UserRoles.Administrator]);
        user.DomainEvents.OfType<UserRolesChangedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ChangePassword_is_rejected_while_the_user_is_suspended()
    {
        var user = ActiveUser();
        user.Suspend("Held");

        user.ChangePassword("new-hash", Now).IsFailure.ShouldBeTrue();
    }

    private static User ActiveUser()
    {
        var user = User.Invite(AnEmail(), "Ada Lovelace", [UserRoles.Member]).Value;
        user.Activate("hashed", Now);
        user.ClearDomainEvents();
        return user;
    }
}
