namespace CleanArchTemplate.Application.Abstractions.Notifications;

public sealed record EmailMessage(
    string To,
    string? ToDisplayName,
    string Subject,
    string HtmlBody,
    string TextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
