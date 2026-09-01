using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace CleanArchTemplate.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext context, IClock clock) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveChainAsync(Guid chainId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        return await context.RefreshTokens
            .Where(t => t.ChainId == chainId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        return await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);

    /// <summary>Bulk delete - these rows are never carried in the change tracker.</summary>
    public Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default) =>
        context.RefreshTokens
            .Where(t => t.ExpiresAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
}
