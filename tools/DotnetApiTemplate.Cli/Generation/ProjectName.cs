using System.Text.RegularExpressions;

namespace DotnetApiTemplate.Cli.Generation;

/// <summary>
/// Validates the value given to <c>--project-name</c>.
/// <para>
/// The name becomes the root namespace, every assembly name and every project file name, so it
/// has to be a legal dotted C# identifier chain. Catching that here produces one clear message
/// instead of a wall of compiler errors after generation.
/// </para>
/// </summary>
public static partial class ProjectName
{
    private static readonly HashSet<string> ReservedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
        "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while",
    };

    public static bool TryValidate(string? candidate, out string normalised, out string? error)
    {
        normalised = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            error = "A project name is required. Pass --project-name, for example: --project-name Acme.Billing";
            return false;
        }

        var value = candidate.Trim().Trim('.');

        if (value.Length > 100)
        {
            error = "The project name must be 100 characters or fewer.";
            return false;
        }

        var segments = value.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            error = $"'{candidate}' is not a usable project name.";
            return false;
        }

        foreach (var segment in segments)
        {
            if (!Segment().IsMatch(segment))
            {
                error = $"'{segment}' is not a valid name segment. Use letters, digits and underscores, "
                      + "starting with a letter or underscore - for example 'Acme.Billing'.";
                return false;
            }

            if (ReservedSegments.Contains(segment))
            {
                error = $"'{segment}' is a C# keyword and cannot be part of a namespace.";
                return false;
            }
        }

        normalised = string.Join('.', segments);
        error = null;
        return true;
    }

    /// <summary>Lowercase, dot-free form used as a sensible default for the Kafka topic prefix.</summary>
    public static string ToDefaultTopicPrefix(string projectName) =>
        string.Join('-', projectName.Split('.', StringSplitOptions.RemoveEmptyEntries))
              .ToLowerInvariant();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Segment();
}
