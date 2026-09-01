namespace DotnetApiTemplate.Cli.CommandLine;

/// <summary>One declared option, with its aliases and the text shown by <c>--help</c>.</summary>
public sealed record OptionDefinition(
    string Name,
    string? Alias,
    string Description,
    bool IsFlag = false,
    string? ValuePlaceholder = null,
    string? DefaultValue = null);

/// <summary>The options a command understands. Also renders that command's help.</summary>
public sealed class OptionSet
{
    private readonly List<OptionDefinition> _options = [];

    public IReadOnlyList<OptionDefinition> Options => _options;

    public OptionSet Add(string name, string description, string? alias = null, string? placeholder = null, string? defaultValue = null)
    {
        _options.Add(new OptionDefinition(name, alias, description, IsFlag: false, placeholder, defaultValue));
        return this;
    }

    public OptionSet AddFlag(string name, string description, string? alias = null)
    {
        _options.Add(new OptionDefinition(name, alias, description, IsFlag: true));
        return this;
    }

    public bool TryResolve(string token, out OptionDefinition option)
    {
        foreach (var candidate in _options)
        {
            if (string.Equals(candidate.Name, token, StringComparison.OrdinalIgnoreCase)
                || (candidate.Alias is not null && string.Equals(candidate.Alias, token, StringComparison.Ordinal)))
            {
                option = candidate;
                return true;
            }
        }

        option = null!;
        return false;
    }

    public void WriteTo(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var rendered = _options
            .Select(option => (Left: Render(option), option.Description, option.DefaultValue))
            .ToList();

        var width = rendered.Count == 0 ? 0 : rendered.Max(entry => entry.Left.Length);

        foreach (var (left, description, defaultValue) in rendered)
        {
            var suffix = defaultValue is null ? string.Empty : $" [default: {defaultValue}]";
            writer.WriteLine($"  {left.PadRight(width)}  {description}{suffix}");
        }
    }

    private static string Render(OptionDefinition option)
    {
        var names = option.Alias is null ? option.Name : $"{option.Alias}, {option.Name}";
        return option.IsFlag ? names : $"{names} <{option.ValuePlaceholder ?? "value"}>";
    }
}
