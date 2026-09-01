using System.Globalization;

namespace CleanArchTemplate.Infrastructure.Configuration;

/// <summary>
/// Minimal <c>.env</c> loader for local development.
/// <para>
/// Compose already injects these values through <c>env_file</c>, so this exists purely so that
/// <c>dotnet run</c> outside a container behaves the same way. Real environment variables always
/// win, which keeps production - where there is no .env file at all - completely unaffected.
/// </para>
/// </summary>
public static class DotEnv
{
    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> looking for a <c>.env</c> file and loads it.
    /// Missing files are not an error.
    /// </summary>
    public static void Load(string? startDirectory = null, string fileName = ".env")
    {
        var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);

            if (File.Exists(candidate))
            {
                LoadFile(candidate);
                return;
            }

            directory = directory.Parent;
        }
    }

    public static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            // Strip one layer of matching quotes, the way a shell would.
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // A real environment variable is authoritative; .env only fills gaps.
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    /// <summary>Reads a variable, falling back to <paramref name="fallback"/> when unset or blank.</summary>
    public static string Get(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public static int GetInt(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
