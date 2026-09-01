using System.Security.Cryptography;
using System.Text;
using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Auth;

/// <summary>
/// A rotating refresh token. Only the hash is stored; presenting an already-rotated token is
/// treated as theft and revokes the whole chain (see <see cref="MarkReused"/>).
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private const int EntropyBytes = 48;

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now, string? createdByIp, Guid chainId)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = now;
        CreatedByIp = createdByIp;
        ChainId = chainId;
    }

    // EF Core materialisation.
    private RefreshToken() => TokenHash = null!;

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    /// <summary>Groups every token derived from one login, so a reuse can revoke the family.</summary>
    public Guid ChainId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public string? CreatedByIp { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public static (RefreshToken Token, string RawValue) Issue(Guid userId, TimeSpan lifetime, DateTimeOffset now, string? createdByIp, Guid? chainId = null)
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(EntropyBytes));
        var token = new RefreshToken(
            Guid.CreateVersion7(),
            userId,
            HashOf(raw),
            now.Add(lifetime),
            now,
            createdByIp,
            chainId ?? Guid.CreateVersion7());

        return (token, raw);
    }

    public Result Rotate(RefreshToken replacement, DateTimeOffset now)
    {
        if (!IsActiveAt(now))
        {
            return Result.Failure(AuthErrors.InvalidRefreshToken);
        }

        RevokedAt = now;
        RevokedReason = "rotated";
        ReplacedByTokenId = replacement.Id;
        return Result.Success();
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

    public void MarkReused(DateTimeOffset now) => Revoke(now, "reuse-detected");

    public static string HashOf(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
