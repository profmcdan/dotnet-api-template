using System.Text.RegularExpressions;

namespace CleanArchTemplate.Application.Common;

/// <summary>
/// Length-first policy in the spirit of NIST SP 800-63B: a long passphrase beats a short one
/// full of symbols, so the composition rules stay deliberately mild.
/// </summary>
public static partial class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 256;

    /// <summary>Substrings so common that requiring anything else is cheap and worthwhile.</summary>
    private static readonly string[] Banned =
    [
        "password", "12345678", "qwerty", "letmein", "welcome", "changeme", "admin123",
    ];

    public static bool IsAcceptable(string? password, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            reason = $"Password must be at least {MinLength} characters long.";
            return false;
        }

        if (password.Length > MaxLength)
        {
            reason = $"Password must be {MaxLength} characters or fewer.";
            return false;
        }

        if (!HasLetter().IsMatch(password) || !HasDigitOrSymbol().IsMatch(password))
        {
            reason = "Password must contain at least one letter and one digit or symbol.";
            return false;
        }

        var lowered = password.ToLowerInvariant();
        if (Array.Exists(Banned, banned => lowered.Contains(banned, StringComparison.Ordinal)))
        {
            reason = "Password contains a well-known phrase and is too easy to guess.";
            return false;
        }

        reason = null;
        return true;
    }

    [GeneratedRegex(@"\p{L}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex HasLetter();

    [GeneratedRegex(@"[\d\p{P}\p{S}]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex HasDigitOrSymbol();
}
