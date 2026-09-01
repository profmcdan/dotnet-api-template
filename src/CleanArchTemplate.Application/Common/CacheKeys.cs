namespace CleanArchTemplate.Application.Common;

/// <summary>
/// One place for every cache key, so invalidation is greppable rather than guesswork.
/// </summary>
public static class CacheKeys
{
    public const string UserPrefix = "user:";
    public const string UserListPrefix = "user-list:";
    public const string SecurityStampPrefix = "security-stamp:";

    public static string User(Guid id) => $"{UserPrefix}{id:N}";

    /// <summary>
    /// Cached per user so the API can check token revocation without a database round-trip on
    /// every request. Must be evicted by anything that bumps the stamp - see <see cref="UserCache"/>.
    /// </summary>
    public static string SecurityStamp(Guid id) => $"{SecurityStampPrefix}{id:N}";

    public static string EmailClaim(string idempotencyKey) => $"email-claim:{idempotencyKey}";

    public static string LoginAttempts(string email) => $"login-attempts:{email}";
}
