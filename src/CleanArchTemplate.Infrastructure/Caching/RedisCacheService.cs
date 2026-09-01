using System.Text.Json;
using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CleanArchTemplate.Infrastructure.Caching;

/// <summary>
/// Redis-backed cache. Every Redis fault is swallowed and logged: a cache outage must cost
/// latency, never correctness, so reads fall through to the factory and writes are best-effort.
/// </summary>
internal sealed class RedisCacheService(
    IConnectionMultiplexer connection,
    IOptions<RedisOptions> options,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RedisOptions _options = options.Value;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        (await TryGetAsync<T>(key)).Value;

    /// <summary>
    /// Distinguishes "not cached" from "cached as default(T)". Without this a cached
    /// <c>0</c> or <c>false</c> is indistinguishable from a miss, and every read of a value type
    /// silently returns the default instead of consulting the source.
    /// </summary>
    private async Task<(bool Found, T? Value)> TryGetAsync<T>(string key)
    {
        try
        {
            var value = await connection.GetDatabase().StringGetAsync(Qualify(key));

            return value.IsNullOrEmpty
                ? (false, default)
                : (true, JsonSerializer.Deserialize<T>((string)value!, SerializerOptions));
        }
        catch (Exception ex) when (ex is RedisException or JsonException or TimeoutException)
        {
            CacheLog.ReadFailed(logger, ex, key);
            return (false, default);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await connection.GetDatabase().StringSetAsync(Qualify(key), payload, ttl ?? _options.DefaultTtl);
        }
        catch (Exception ex) when (ex is RedisException or JsonException or TimeoutException)
        {
            CacheLog.WriteFailed(logger, ex, key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var (found, cached) = await TryGetAsync<T>(key);
        if (found)
        {
            return cached!;
        }

        var value = await factory(cancellationToken);

        if (value is not null)
        {
            await SetAsync(key, value, ttl, cancellationToken);
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().KeyDeleteAsync(Qualify(key));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            CacheLog.EvictFailed(logger, ex, key);
        }
    }

    /// <summary>
    /// Scans rather than using <c>KEYS</c>, which blocks the server. Still O(keyspace) - reserve
    /// it for coarse invalidation such as clearing a list cache after a write.
    /// </summary>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{Qualify(prefix)}*";
            var database = connection.GetDatabase();

            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);
                if (server.IsReplica || !server.IsConnected)
                {
                    continue;
                }

                await foreach (var key in server.KeysAsync(database.Database, pattern, pageSize: 250).WithCancellation(cancellationToken))
                {
                    await database.KeyDeleteAsync(key);
                }
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            CacheLog.EvictFailed(logger, ex, prefix);
        }
    }

    private string Qualify(string key) => $"{_options.InstanceName}:{key}";
}

internal static partial class CacheLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "Cache read failed for {Key}; falling through to the source")]
    public static partial void ReadFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "Cache write failed for {Key}")]
    public static partial void WriteFailed(ILogger logger, Exception exception, string key);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Cache eviction failed for {Key}")]
    public static partial void EvictFailed(ILogger logger, Exception exception, string key);
}
