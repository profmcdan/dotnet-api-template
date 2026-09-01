using System.Text.RegularExpressions;
using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users;

/// <summary>
/// A normalised, syntactically valid email address. Normalisation is lower-casing and
/// trimming only - the local part is never rewritten, because that is provider specific.
/// </summary>
public sealed partial record Email
{
    public const int MaxLength = 320;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return EmailErrors.Empty;
        }

        var normalised = candidate.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            return EmailErrors.TooLong;
        }

        return Pattern().IsMatch(normalised)
            ? new Email(normalised)
            : EmailErrors.Invalid(candidate);
    }

    public string Domain => Value[(Value.IndexOf('@', StringComparison.Ordinal) + 1)..];

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Pattern();
}

public static class EmailErrors
{
    public static readonly Error Empty =
        Error.Validation("email.empty", "Email address is required.");

    public static readonly Error TooLong =
        Error.Validation("email.too_long", $"Email address must be {Email.MaxLength} characters or fewer.");

    public static Error Invalid(string value) =>
        Error.Validation("email.invalid", $"'{value}' is not a valid email address.");
}
