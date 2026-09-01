using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokensResponse>, ITransactionalRequest;

internal sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

/// <summary>
/// Rotates a refresh token. Presenting one that has already been rotated is treated as theft:
/// the entire chain descended from that login is revoked, which logs out both the attacker and
/// the legitimate holder rather than silently letting the attacker continue.
/// </summary>
internal sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<RefreshTokenCommand, AuthTokensResponse>
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var existing = await refreshTokens.GetByHashAsync(Domain.Auth.RefreshToken.HashOf(request.RefreshToken), cancellationToken);

        if (existing is null)
        {
            return Result.Failure<AuthTokensResponse>(AuthErrors.InvalidRefreshToken);
        }

        if (!existing.IsActiveAt(now))
        {
            // Already rotated or revoked, yet someone still holds it - burn the family.
            foreach (var sibling in await refreshTokens.GetActiveChainAsync(existing.ChainId, cancellationToken))
            {
                sibling.MarkReused(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokensResponse>(AuthErrors.RefreshTokenReuse);
        }

        var user = await users.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.CanAuthenticate)
        {
            existing.Revoke(now, "user-not-authenticatable");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokensResponse>(UserErrors.NotActive);
        }

        var (replacement, rawRefreshToken) = Domain.Auth.RefreshToken.Issue(
            user.Id, tokenService.RefreshTokenLifetime, now, currentUser.IpAddress, existing.ChainId);

        var rotated = existing.Rotate(replacement, now);
        if (rotated.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(rotated.Error);
        }

        refreshTokens.Add(replacement);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return Result.Success(new AuthTokensResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            LoginCommandHandler.ToResponse(user)));
    }
}
