using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Application.Abstractions.Notifications;
using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Contracts.Notifications;
using CleanArchTemplate.Infrastructure.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Worker.Consumers;

/// <summary>
/// The only place email actually leaves the system: render a template, hand it to SMTP.
/// <para>
/// Kafka delivery is at-least-once, so the idempotency key is claimed before sending and released
/// if the send fails - the retry then legitimately re-claims it. The failure mode this trades for
/// is a duplicate email when a send succeeds but the process dies before committing the offset,
/// which is much better than an invitation that silently never arrives.
/// </para>
/// </summary>
internal sealed class EmailDispatchConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger<EmailDispatchConsumer> logger)
    : KafkaConsumerBase<EmailRequestedIntegrationEvent>(scopeFactory, kafkaOptions, topics, logger)
{
    protected override string LogicalTopic => Topics.EmailRequested;

    protected override string GroupSuffix => "email-dispatch";

    protected override string? DeadLetterTopic => Topics.EmailDeadLetter;

    protected override async Task HandleAsync(EmailRequestedIntegrationEvent message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var deduplicator = services.GetRequiredService<IEmailDeduplicator>();
        var renderer = services.GetRequiredService<IEmailTemplateRenderer>();
        var sender = services.GetRequiredService<IEmailSender>();
        var log = services.GetRequiredService<ILogger<EmailDispatchConsumer>>();

        if (!await deduplicator.TryClaimAsync(message.IdempotencyKey, cancellationToken))
        {
            WorkerLog.DuplicateEmailSkipped(log, message.IdempotencyKey);
            return;
        }

        try
        {
            var rendered = renderer.Render(message.TemplateId, message.Model);

            await sender.SendAsync(
                new EmailMessage(message.To, message.ToDisplayName, rendered.Subject, rendered.HtmlBody, rendered.TextBody),
                cancellationToken);
        }
        catch
        {
            // Release the claim so the redelivery is allowed to try again.
            await deduplicator.ReleaseAsync(message.IdempotencyKey, CancellationToken.None);
            throw;
        }
    }
}
