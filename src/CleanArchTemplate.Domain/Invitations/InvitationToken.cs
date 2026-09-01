using System.Security.Cryptography;
using System.Text;

namespace CleanArchTemplate.Domain.Invitations;

/// <summary>
/// A single-use invitation secret. The raw value leaves the process exactly once - in the
/// invitation email - and only its SHA-256 hash is ever persisted on the aggregate.
/// </summary>
public sealed record InvitationToken
{
    private const int EntropyBytes = 32;

    private InvitationToken(string raw, string hash)
    {
        Raw = raw;
        Hash = hash;
    }

    public string Raw { get; }

    public string Hash { get; }

    public static InvitationToken Issue()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(EntropyBytes));
        return new InvitationToken(raw, HashOf(raw));
    }

    public static string HashOf(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
