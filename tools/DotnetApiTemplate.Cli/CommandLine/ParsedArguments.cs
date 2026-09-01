using System.Globalization;

namespace DotnetApiTemplate.Cli.CommandLine;

/// <summary>
/// A very small argument parser.
/// <para>
/// Hand-rolled on purpose: this tool is installed globally, and a parser for a handful of flags
/// is not worth a dependency that every user then has to trust and carry. It accepts
/// <c>--key value</c>, <c>--key=value</c>, short aliases and bare boolean flags, which is the
/// whole surface the commands need.
/// </para>
/// </summary>
public sealed class ParsedArguments
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positional = [];
    private readonly List<string> _unknown = [];

    private ParsedArguments()
    {
    }

    public IReadOnlyList<string> Positional => _positional;

    /// <summary>Options supplied but not declared by the command. Reported rather than ignored.</summary>
    public IReadOnlyList<string> Unrecognised => _unknown;

    public static ParsedArguments Parse(IEnumerable<string> args, OptionSet declared)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(declared);

        var parsed = new ParsedArguments();
        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var token = enumerator.Current;

            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!token.StartsWith('-'))
            {
                parsed._positional.Add(token);
                continue;
            }

            var separator = token.IndexOf('=', StringComparison.Ordinal);
            var name = separator >= 0 ? token[..separator] : token;
            var inlineValue = separator >= 0 ? token[(separator + 1)..] : null;

            if (!declared.TryResolve(name, out var option))
            {
                parsed._unknown.Add(name);
                continue;
            }

            if (option.IsFlag)
            {
                // `--flag` and `--flag=false` are both meaningful.
                parsed._options[option.Name] = inlineValue ?? bool.TrueString;
                continue;
            }

            if (inlineValue is not null)
            {
                parsed._options[option.Name] = inlineValue;
                continue;
            }

            if (!enumerator.MoveNext())
            {
                throw new CommandLineException($"Option '{name}' expects a value.");
            }

            parsed._options[option.Name] = enumerator.Current;
        }

        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? GetString(string name, string? fallback = null) =>
        _options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    public bool GetFlag(string name, bool fallback = false)
    {
        if (!_options.TryGetValue(name, out var value))
        {
            return fallback;
        }

        return value is null || !bool.TryParse(value, out var parsed) || parsed;
    }

    public int GetInt(string name, int fallback)
    {
        if (!_options.TryGetValue(name, out var value) || value is null)
        {
            return fallback;
        }

        if (!int.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new CommandLineException($"Option '{name}' expects a whole number, but got '{value}'.");
        }

        return parsed;
    }
}

/// <summary>A usage error. Reported to the user as a message, never as a stack trace.</summary>
public sealed class CommandLineException(string message) : Exception(message);
