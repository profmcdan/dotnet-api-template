using CleanArchTemplate.Application.Abstractions.Security;

namespace CleanArchTemplate.Infrastructure.Security;

/// <summary>
/// bcrypt at cost 12. The cost is embedded in the hash, so raising it later still verifies old
/// hashes and <see cref="NeedsRehash"/> upgrades them on the user's next successful sign-in.
/// </summary>
internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Verified against when no user exists, so a miss costs the same time as a wrong password
    /// and the endpoint cannot be used to enumerate registered addresses.
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalising-placeholder", WorkFactor);

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash ?? DummyHash) && hash is not null;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, WorkFactor);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return true;
        }
    }
}
