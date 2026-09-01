using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Application.Auth.Commands;

/// <summary>
/// Revokes the presented refresh token, or every session for the caller when
/// <paramref name="AllSessions"/> is set. Always reports success - telling a caller that the
/// token they handed in was already dead leaks nothing useful and only complicates clients.
/// </summary>
public sealed record LogoutCommand(string? RefreshToken, bool AllSessions = false) : ICommand;

internal sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> HandleAsync(LogoutCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (request.AllSessions && currentUser.UserId is { } userId)
        {
            foreach (var token in await refreshTokens.GetActiveForUserAsync(userId, cancellationToken))
            {
                token.Revoke(now, "logout-all");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var token = await refreshTokens.GetByHashAsync(RefreshToken.HashOf(request.RefreshToken), cancellationToken);

            // Only the owner may revoke, so a leaked token cannot be used to sign someone else out.
            if (token is not null && (currentUser.UserId is null || token.UserId == currentUser.UserId))
            {
                token.Revoke(now, "logout");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
