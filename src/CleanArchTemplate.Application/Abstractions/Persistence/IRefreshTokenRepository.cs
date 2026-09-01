using CleanArchTemplate.Domain.Auth;

namespace CleanArchTemplate.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>All still-active tokens in one login chain - the blast radius of a reuse.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveChainAsync(Guid chainId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(RefreshToken token);

    /// <summary>Housekeeping for the retention worker.</summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
