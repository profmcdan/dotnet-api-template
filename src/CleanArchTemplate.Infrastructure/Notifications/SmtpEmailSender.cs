using CleanArchTemplate.Application.Abstractions.Notifications;
using CleanArchTemplate.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CleanArchTemplate.Infrastructure.Notifications;

/// <summary>
/// SMTP transport. A connection is opened per send rather than pooled: throughput here is bounded
/// by the consumer's concurrency, and a long-lived SMTP session is far more trouble than it saves.
/// Every message carries both an HTML and a plain-text part.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToDisplayName ?? message.To, message.To));
        mime.Subject = message.Subject;

        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)TimeSpan.FromSeconds(_options.TimeoutSeconds).TotalMilliseconds,
        };

        var socketOptions = Enum.TryParse<SecureSocketOptions>(_options.SecureSocketOptions, ignoreCase: true, out var parsed)
            ? parsed
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        EmailLog.Sent(logger, message.To, message.Subject);
    }
}

internal static partial class EmailLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Email sent to {Recipient}: {Subject}")]
    public static partial void Sent(ILogger logger, string recipient, string subject);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Email {IdempotencyKey} already sent; skipping duplicate delivery")]
    public static partial void DuplicateSkipped(ILogger logger, string idempotencyKey);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Error, Message = "Email send failed for {Recipient} using template {TemplateId}")]
    public static partial void SendFailed(ILogger logger, Exception exception, string recipient, string templateId);
}
