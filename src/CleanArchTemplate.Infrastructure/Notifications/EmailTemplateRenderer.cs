using System.Collections.Frozen;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CleanArchTemplate.Application.Abstractions.Notifications;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Notifications;

/// <summary>
/// Renders the embedded email templates with a deliberately small mustache subset:
/// <c>{{Key}}</c> substitution and <c>{{#Key}}...{{/Key}}</c> blocks that appear only when the key
/// has a value. A full template engine would be a much larger attack surface for something that
/// only ever renders four trusted files.
/// <para>
/// Model values are HTML-encoded on the way in; template files are the only trusted input.
/// </para>
/// </summary>
internal sealed partial class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string LayoutName = "_layout";
    private const string ResourcePrefix = "CleanArchTemplate.Infrastructure.Notifications.Templates.";

    private readonly FrozenDictionary<string, EmailTemplate> _templates;
    private readonly AppOptions _app;

    public EmailTemplateRenderer(IOptions<AppOptions> appOptions)
    {
        ArgumentNullException.ThrowIfNull(appOptions);
        _app = appOptions.Value;
        _templates = LoadTemplates();
    }

    public bool TemplateExists(string templateId) => _templates.ContainsKey(templateId);

    public RenderedEmail Render(string templateId, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new InvalidOperationException(
                $"No email template '{templateId}'. Known templates: {string.Join(", ", _templates.Keys)}.");
        }

        if (!_templates.TryGetValue(LayoutName, out var layout))
        {
            throw new InvalidOperationException("The '_layout' email template is missing from the assembly resources.");
        }

        // Template metadata supplies defaults; the caller's model wins on conflict.
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = _app.Name,
            ["SupportUrl"] = _app.SupportUrl,
            ["Year"] = DateTimeOffset.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ActionLabel"] = template.ActionLabel ?? string.Empty,
            ["ActionHint"] = template.ActionHint ?? string.Empty,
            ["Preheader"] = template.Preheader ?? string.Empty,
        };

        foreach (var (key, value) in model)
        {
            values[key] = value;
        }

        var htmlValues = values.ToDictionary(pair => pair.Key, pair => WebUtility.HtmlEncode(pair.Value), StringComparer.OrdinalIgnoreCase);

        var subject = Substitute(template.Subject, values, encode: false);
        htmlValues["Subject"] = WebUtility.HtmlEncode(subject);
        values["Subject"] = subject;

        htmlValues["Preheader"] = WebUtility.HtmlEncode(Substitute(values.GetValueOrDefault("Preheader", string.Empty), values, encode: false));

        htmlValues["Content"] = Substitute(template.Html, htmlValues, encode: false);
        values["Content"] = Substitute(template.Text, values, encode: false);

        return new RenderedEmail(
            subject,
            Substitute(layout.Html, htmlValues, encode: false),
            Substitute(layout.Text, values, encode: false));
    }

    /// <summary>
    /// Resolves conditional blocks first, then placeholders. Values are pre-encoded by the caller,
    /// so <paramref name="encode"/> stays false on every current path.
    /// </summary>
    private static string Substitute(string template, Dictionary<string, string> values, bool encode)
    {
        var withBlocks = ConditionalBlock().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            var present = values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
            return present ? match.Groups["body"].Value : string.Empty;
        });

        return Placeholder().Replace(withBlocks, match =>
        {
            var key = match.Groups["key"].Value;
            if (!values.TryGetValue(key, out var value))
            {
                // Unknown placeholders render empty rather than leaking "{{Foo}}" into a customer's inbox.
                return string.Empty;
            }

            return encode ? WebUtility.HtmlEncode(value) : value;
        });
    }

    private static FrozenDictionary<string, EmailTemplate> LoadTemplates()
    {
        var assembly = typeof(EmailTemplateRenderer).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToArray();

        var ids = names
            .Select(name => name[ResourcePrefix.Length..])
            .Select(name => name[..name.LastIndexOf('.')])
            .Distinct(StringComparer.Ordinal);

        var templates = new Dictionary<string, EmailTemplate>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in ids)
        {
            var html = ReadResource(assembly, $"{ResourcePrefix}{id}.html");
            var text = ReadResource(assembly, $"{ResourcePrefix}{id}.txt") ?? string.Empty;

            if (html is null)
            {
                continue;
            }

            var (metadata, body) = SplitMetadata(html);
            var (_, textBody) = SplitMetadata(text);

            templates[id] = new EmailTemplate(
                Subject: metadata.GetValueOrDefault("subject", id),
                Preheader: metadata.GetValueOrDefault("preheader"),
                ActionLabel: metadata.GetValueOrDefault("action"),
                ActionHint: metadata.GetValueOrDefault("actionHint"),
                Html: body,
                Text: textBody);
        }

        return templates.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Strips the leading <c>@key: value</c> header lines from a template file.</summary>
    private static (Dictionary<string, string> Metadata, string Body) SplitMetadata(string content)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = new StringBuilder();
        var inHeader = true;

        foreach (var line in content.Split('\n'))
        {
            if (inHeader && line.StartsWith('@'))
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator > 1)
                {
                    metadata[line[1..separator].Trim()] = line[(separator + 1)..].Trim();
                    continue;
                }
            }

            if (inHeader && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            inHeader = false;
            body.Append(line).Append('\n');
        }

        return (metadata, body.ToString().TrimEnd('\n'));
    }

    private static string? ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.CultureInvariant | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\{\{#\s*(?<key>[A-Za-z0-9_]+)\s*\}\}(?<body>.*?)\{\{/\s*\k<key>\s*\}\}", RegexOptions.Singleline | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConditionalBlock();

    private sealed record EmailTemplate(
        string Subject,
        string? Preheader,
        string? ActionLabel,
        string? ActionHint,
        string Html,
        string Text);
}
