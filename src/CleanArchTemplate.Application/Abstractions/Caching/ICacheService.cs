namespace CleanArchTemplate.Application.Abstractions.Caching;

/// <summary>
/// Distributed cache with a stampede-safe <see cref="GetOrSetAsync{T}"/>. Every failure mode is
/// swallowed and logged: a cache outage must degrade latency, never correctness.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
