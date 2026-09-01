using CleanArchTemplate.Application.Abstractions.Notifications;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CleanArchTemplate.Infrastructure.Caching;

/// <summary>
/// Turns at-least-once Kafka delivery into at-most-once sending by claiming the idempotency key
/// with SET NX before the send. If Redis is unreachable the claim is granted: a duplicate email
/// is a far better failure than a silently dropped invitation.
/// </summary>
internal sealed class RedisEmailDeduplicator(
    IConnectionMultiplexer connection,
    IOptions<RedisOptions> options,
    ILogger<RedisEmailDeduplicator> logger) : IEmailDeduplicator
{
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromDays(3);

    private readonly RedisOptions _options = options.Value;

    public async Task<bool> TryClaimAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        try
        {
            return await connection.GetDatabase()
                .StringSetAsync(Qualify(idempotencyKey), "1", ClaimLifetime, When.NotExists);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            CacheLog.WriteFailed(logger, ex, idempotencyKey);
            return true;
        }
    }

    public async Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().KeyDeleteAsync(Qualify(idempotencyKey));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            CacheLog.EvictFailed(logger, ex, idempotencyKey);
        }
    }

    private string Qualify(string key) => $"{_options.InstanceName}:{CacheKeys.EmailClaim(key)}";
}
