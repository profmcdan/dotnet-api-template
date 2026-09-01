using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Application.Users.Commands;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using CleanArchTemplate.Domain.Users.Events;
using MsOptions = Microsoft.Extensions.Options.Options;
using NSubstitute;

namespace CleanArchTemplate.Application.UnitTests.Users;

public sealed class InviteUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeCache _cache = new();
    private readonly TestCurrentUser _currentUser = new();
    private readonly User _inviter = User.CreateActive(
        Email.Create("admin@example.com").Value, "Root Admin", "hash", [UserRoles.Administrator], Now).Value;

    private InviteUserCommandHandler CreateHandler()
    {
        _currentUser.UserId = _inviter.Id;
        _users.GetByIdAsync(_inviter.Id, Arg.Any<CancellationToken>()).Returns(_inviter);

        return new InviteUserCommandHandler(
            _users,
            _invitations,
            _unitOfWork,
            _currentUser,
            new FakeClock(Now),
            _cache,
            MsOptions.Create(new InvitationOptions { LifetimeDays = 7 }));
    }

    [Fact]
    public async Task Invites_a_user_and_persists_the_matching_invitation()
    {
        _users.EmailExistsAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler();

        User? addedUser = null;
        Invitation? addedInvitation = null;
        _users.When(r => r.Add(Arg.Any<User>())).Do(call => addedUser = call.Arg<User>());
        _invitations.When(r => r.Add(Arg.Any<Invitation>())).Do(call => addedInvitation = call.Arg<Invitation>());

        var result = await handler.HandleAsync(
            new InviteUserCommand("New.Person@Example.com", "New Person", [UserRoles.Member]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("new.person@example.com");
        result.Value.Status.ShouldBe(UserStatus.Invited);

        addedUser.ShouldNotBeNull();
        addedInvitation.ShouldNotBeNull();
        addedInvitation.UserId.ShouldBe(addedUser.Id);
        addedInvitation.ExpiresAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public async Task Raises_the_invite_event_carrying_the_raw_token_for_the_email()
    {
        _users.EmailExistsAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler();

        User? addedUser = null;
        Invitation? addedInvitation = null;
        _users.When(r => r.Add(Arg.Any<User>())).Do(call => addedUser = call.Arg<User>());
        _invitations.When(r => r.Add(Arg.Any<Invitation>())).Do(call => addedInvitation = call.Arg<Invitation>());

        await handler.HandleAsync(
            new InviteUserCommand("new@example.com", "New Person", [UserRoles.Member]), TestContext.Current.CancellationToken);

        var invited = addedUser.ShouldNotBeNull().DomainEvents.OfType<UserInvitedDomainEvent>().ShouldHaveSingleItem();
        invited.InvitedByName.ShouldBe("Root Admin");

        // The event carries the raw token; only its hash is ever persisted on the aggregate.
        InvitationToken.HashOf(invited.InvitationToken).ShouldBe(addedInvitation.ShouldNotBeNull().TokenHash);
    }

    [Fact]
    public async Task Rejects_an_address_that_is_already_registered()
    {
        _users.EmailExistsAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new InviteUserCommand("taken@example.com", "Taken", [UserRoles.Member]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("user.email_in_use");
        _users.DidNotReceive().Add(Arg.Any<User>());
    }

    [Fact]
    public async Task Rejects_a_malformed_address_before_touching_the_database()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            new InviteUserCommand("not-an-email", "Nobody", [UserRoles.Member]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evicts_the_cached_user_list_so_the_new_invitee_shows_up_immediately()
    {
        _users.EmailExistsAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler();

        await handler.HandleAsync(
            new InviteUserCommand("new@example.com", "New Person", [UserRoles.Member]), TestContext.Current.CancellationToken);

        _cache.RemovedKeys.ShouldContain(CacheKeys.UserListPrefix);
    }

    [Fact]
    public async Task Requires_an_authenticated_inviter()
    {
        var handler = CreateHandler();
        _currentUser.UserId = null;

        var result = await handler.HandleAsync(
            new InviteUserCommand("new@example.com", "New Person", [UserRoles.Member]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }
}
