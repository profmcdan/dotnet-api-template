using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Auth.Commands;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using NSubstitute;

namespace CleanArchTemplate.Application.UnitTests.Auth;

public sealed class AcceptInvitationCommandHandlerTests
{
    private const string GoodPassword = "correct-horse-battery-1";

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IInvitationRepository _invitations = Substitute.For<IInvitationRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeCache _cache = new();

    private AcceptInvitationCommandHandler CreateHandler() => new(
        _invitations,
        _users,
        _refreshTokens,
        _unitOfWork,
        new TestPasswordHasher(),
        new TestTokenService(),
        new TestCurrentUser { UserId = null },
        _cache,
        new FakeClock(Now));

    private (User User, Invitation Invitation, InvitationToken Token) Arrange()
    {
        var inviter = User.CreateActive(
            Email.Create("admin@example.com").Value, "Root Admin", "hash", [UserRoles.Administrator], Now).Value;

        var issued = UserInvitationService.Invite(
            Email.Create("invitee@example.com").Value,
            "Ada Lovelace",
            [UserRoles.Member],
            inviter,
            new InvitationOptions().Lifetime,
            Now).Value;

        _invitations.GetByTokenHashAsync(issued.Invitation.TokenHash, Arg.Any<CancellationToken>())
            .Returns(issued.Invitation);
        _users.GetByIdAsync(issued.User.Id, Arg.Any<CancellationToken>()).Returns(issued.User);

        return (issued.User, issued.Invitation, issued.Token);
    }

    [Fact]
    public async Task Activates_the_user_consumes_the_invitation_and_signs_them_in()
    {
        var (user, invitation, token) = Arrange();

        var result = await CreateHandler().HandleAsync(
            new AcceptInvitationCommand(token.Raw, GoodPassword, null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate.ShouldBeTrue();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        _refreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Optionally_updates_the_name_the_invitee_prefers()
    {
        var (user, _, token) = Arrange();

        await CreateHandler().HandleAsync(
            new AcceptInvitationCommand(token.Raw, GoodPassword, "Augusta King"), TestContext.Current.CancellationToken);

        user.FullName.ShouldBe("Augusta King");
    }

    [Fact]
    public async Task Rejects_an_unknown_token_with_an_opaque_error()
    {
        Arrange();
        _invitations.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Invitation?)null);

        var result = await CreateHandler().HandleAsync(
            new AcceptInvitationCommand("made-up-token", GoodPassword, null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InvitationErrors.InvalidToken);
    }

    [Fact]
    public async Task Rejects_a_token_that_has_already_been_used()
    {
        var (_, invitation, token) = Arrange();
        invitation.Accept(Now);

        var result = await CreateHandler().HandleAsync(
            new AcceptInvitationCommand(token.Raw, GoodPassword, null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InvitationErrors.AlreadyAccepted);
    }

    [Fact]
    public async Task Rejects_a_password_that_fails_the_policy()
    {
        var (user, _, token) = Arrange();

        var result = await CreateHandler().HandleAsync(
            new AcceptInvitationCommand(token.Raw, "short", null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AuthErrors.WeakPassword.Code);
        user.Status.ShouldBe(UserStatus.Invited);
    }
}
