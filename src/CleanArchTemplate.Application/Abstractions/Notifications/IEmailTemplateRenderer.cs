namespace CleanArchTemplate.Application.Abstractions.Notifications;

public sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Renders a template id plus a flat model into a ready-to-send email. Implementations must
/// HTML-encode every model value - templates are trusted, models never are.
/// </summary>
public interface IEmailTemplateRenderer
{
    RenderedEmail Render(string templateId, IReadOnlyDictionary<string, string> model);

    bool TemplateExists(string templateId);
}
