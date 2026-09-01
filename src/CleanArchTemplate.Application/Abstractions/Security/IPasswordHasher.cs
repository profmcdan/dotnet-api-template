namespace CleanArchTemplate.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>Constant-time verification. <paramref name="hash"/> may be null for invited users.</summary>
    bool Verify(string password, string? hash);

    /// <summary>True when the stored hash used a weaker work factor and should be upgraded on next login.</summary>
    bool NeedsRehash(string hash);
}
