using CleanArchTemplate.Application.Abstractions.Caching;

namespace CleanArchTemplate.Application.Common;

/// <summary>
/// Evicts everything cached about one user.
/// <para>
/// Kept as a single call because the security stamp is the entry that actually matters: forget to
/// drop it and a suspended user keeps working until the cached copy expires. Handlers should never
/// evict user keys individually.
/// </para>
/// </summary>
public static class UserCache
{
    public static async Task InvalidateAsync(ICacheService cache, Guid userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        await cache.RemoveAsync(CacheKeys.User(userId), cancellationToken);
        await cache.RemoveAsync(CacheKeys.SecurityStamp(userId), cancellationToken);
        await cache.RemoveByPrefixAsync(CacheKeys.UserListPrefix, cancellationToken);
    }
}
