using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DotnetApiTemplate.Cli.Generation;

/// <summary>
/// Produces the generated project's <c>.env</c> from its <c>.env.sample</c>, replacing the
/// placeholder secrets with real random ones.
/// <para>
/// This exists because the single most common way to get a template like this wrong is to ship
/// the sample signing key. Generating it at scaffold time means the placeholder never survives
/// long enough to reach a running system.
/// </para>
/// </summary>
public static partial class EnvironmentFile
{
    public sealed record Result(string Path, bool Created, string? SkipReason);

    public static Result Create(string projectDirectory)
    {
        var sample = Path.Combine(projectDirectory, ".env.sample");
        var target = Path.Combine(projectDirectory, ".env");

        if (!File.Exists(sample))
        {
            return new Result(target, false, "no .env.sample was generated");
        }

        if (File.Exists(target))
        {
            return new Result(target, false, ".env already exists and was left untouched");
        }

        var signingKey = RandomBase64(48);
        var databasePassword = RandomPassword(24);

        var lines = File.ReadAllLines(sample);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith('#') || !line.Contains('=', StringComparison.Ordinal))
            {
                continue;
            }

            lines[i] = line switch
            {
                _ when line.StartsWith("JWT__SIGNINGKEY=", StringComparison.Ordinal) =>
                    $"JWT__SIGNINGKEY={signingKey}",

                _ when line.StartsWith("POSTGRES_PASSWORD=", StringComparison.Ordinal) =>
                    $"POSTGRES_PASSWORD={databasePassword}",

                // The connection string embeds the same password; the two must not drift.
                _ when line.StartsWith("DATABASE__CONNECTIONSTRING=", StringComparison.Ordinal) =>
                    PasswordField().Replace(line, $"Password={databasePassword}"),

                _ => line,
            };
        }

        File.WriteAllLines(target, lines);
        return new Result(target, true, null);
    }

    private static string RandomBase64(int bytes) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>
    /// Alphanumeric only. Connection strings, shell files and URLs each quote punctuation
    /// differently, and a password that needs escaping in three places is a support ticket.
    /// </summary>
    private static string RandomPassword(int length)
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }

    [GeneratedRegex(@"Password=[^;]*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex PasswordField();
}
