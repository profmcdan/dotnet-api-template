using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Auth.Commands;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Users;
using MsOptions = Microsoft.Extensions.Options.Options;
using NSubstitute;

namespace CleanArchTemplate.Application.UnitTests.Auth;

public sealed class LoginCommandHandlerTests
{
    private const string Password = "correct-horse-battery-1";

    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeCache _cache = new();
    private readonly SecurityOptions _security = new() { MaxFailedLoginAttempts = 3, LockoutWindowMinutes = 15 };

    private LoginCommandHandler CreateHandler() => new(
        _users,
        _refreshTokens,
        _unitOfWork,
        new TestPasswordHasher(),
        new TestTokenService(),
        new TestCurrentUser(),
        _cache,
        new FakeClock(Now),
        MsOptions.Create(_security));

    private User ActiveUser()
    {
        var user = User.CreateActive(
            Email.Create("person@example.com").Value, "Ada", new TestPasswordHasher().Hash(Password), [UserRoles.Member], Now).Value;

        _users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Issues_a_token_pair_for_valid_credentials()
    {
        var user = ActiveUser();

        var result = await CreateHandler().HandleAsync(
            new LoginCommand("person@example.com", Password), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.User.Id.ShouldBe(user.Id);
        user.LastLoginAt.ShouldBe(Now);
        _refreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Reports_the_same_error_for_a_wrong_password_and_an_unknown_address()
    {
        ActiveUser();
        var wrongPassword = await CreateHandler().HandleAsync(
            new LoginCommand("person@example.com", "wrong-password-here"), TestContext.Current.CancellationToken);

        _users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        var unknownUser = await CreateHandler().HandleAsync(
            new LoginCommand("nobody@example.com", Password), TestContext.Current.CancellationToken);

        wrongPassword.Error.ShouldBe(UserErrors.InvalidCredentials);
        unknownUser.Error.ShouldBe(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Refuses_a_suspended_account_even_with_the_right_password()
    {
        var user = ActiveUser();
        user.Suspend("Policy violation");

        var result = await CreateHandler().HandleAsync(
            new LoginCommand("person@example.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotActive);
    }

    [Fact]
    public async Task Refuses_an_invited_user_who_has_not_accepted_yet()
    {
        var user = User.Invite(Email.Create("pending@example.com").Value, "Pending", [UserRoles.Member]).Value;
        _users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().HandleAsync(
            new LoginCommand("pending@example.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Locks_out_after_too_many_failures_in_the_window()
    {
        ActiveUser();
        var handler = CreateHandler();

        for (var attempt = 0; attempt < _security.MaxFailedLoginAttempts; attempt++)
        {
            await handler.HandleAsync(new LoginCommand("person@example.com", "wrong-password-x"), TestContext.Current.CancellationToken);
        }

        var result = await handler.HandleAsync(new LoginCommand("person@example.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.TooManyAttempts);
    }

    [Fact]
    public async Task Clears_the_failure_counter_after_a_successful_sign_in()
    {
        ActiveUser();
        var handler = CreateHandler();

        await handler.HandleAsync(new LoginCommand("person@example.com", "wrong-password-x"), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new LoginCommand("person@example.com", Password), TestContext.Current.CancellationToken);

        var attempts = await _cache.GetAsync<int?>(
            CacheKeys.LoginAttempts("person@example.com"), TestContext.Current.CancellationToken);

        attempts.ShouldBeNull();
    }
}
