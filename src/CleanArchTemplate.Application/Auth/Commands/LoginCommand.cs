using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Application.Users.Queries;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokensResponse>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>
/// Exchanges credentials for a token pair. Every rejection returns the same
/// <see cref="UserErrors.InvalidCredentials"/> and costs roughly the same time, so the endpoint
/// cannot be used to discover which addresses are registered.
/// </summary>
internal sealed class LoginCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    ICacheService cache,
    IClock clock,
    IOptions<SecurityOptions> securityOptions) : ICommandHandler<LoginCommand, AuthTokensResponse>
{
    private readonly SecurityOptions _security = securityOptions.Value;

    public async Task<Result<AuthTokensResponse>> HandleAsync(LoginCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Domain.Users.Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(UserErrors.InvalidCredentials);
        }

        var email = emailResult.Value;
        var attemptsKey = CacheKeys.LoginAttempts(email.Value);

        var attempts = await cache.GetAsync<int?>(attemptsKey, cancellationToken) ?? 0;
        if (attempts >= _security.MaxFailedLoginAttempts)
        {
            return Result.Failure<AuthTokensResponse>(AuthErrors.TooManyAttempts);
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);

        // Verify unconditionally: a missing user must cost the same as a wrong password.
        var passwordMatches = passwordHasher.Verify(request.Password, user?.PasswordHash);

        if (user is null || !passwordMatches || !user.CanAuthenticate)
        {
            await cache.SetAsync(attemptsKey, attempts + 1, _security.LockoutWindow, cancellationToken);

            // Distinguish "wrong password" from "account disabled" only once the password checks out.
            return Result.Failure<AuthTokensResponse>(
                user is not null && passwordMatches ? UserErrors.NotActive : UserErrors.InvalidCredentials);
        }

        await cache.RemoveAsync(attemptsKey, cancellationToken);

        var now = clock.UtcNow;

        // Opportunistically upgrade hashes written under an older work factor.
        if (user.PasswordHash is { } hash && passwordHasher.NeedsRehash(hash))
        {
            user.ChangePassword(passwordHasher.Hash(request.Password), now);
        }

        user.RecordLogin(now);

        var (refreshToken, rawRefreshToken) = RefreshToken.Issue(
            user.Id, tokenService.RefreshTokenLifetime, now, currentUser.IpAddress);

        refreshTokens.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return Result.Success(new AuthTokensResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            ToResponse(user)));
    }

    internal static UserResponse ToResponse(User user) => new(
        user.Id,
        user.Email.Value,
        user.FullName,
        user.Status,
        user.Roles,
        user.CreatedAt,
        user.LastLoginAt,
        user.SuspensionReason);
}
