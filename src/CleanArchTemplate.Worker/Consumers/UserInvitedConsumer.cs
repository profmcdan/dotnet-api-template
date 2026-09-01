using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Contracts.Notifications;
using CleanArchTemplate.Contracts.Users;
using CleanArchTemplate.Infrastructure.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Worker.Consumers;

/// <summary>
/// Turns a "user invited" fact into an email request.
/// <para>
/// This deliberately does not send the email itself. Keeping "decide what to send" separate from
/// "send it" means a broken SMTP server backs up one topic instead of blocking the user events,
/// and the email topic can be replayed on its own once the transport recovers.
/// </para>
/// </summary>
internal sealed class UserInvitedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger<UserInvitedConsumer> logger)
    : KafkaConsumerBase<UserInvitedIntegrationEvent>(scopeFactory, kafkaOptions, topics, logger)
{
    protected override string LogicalTopic => Topics.UserInvited;

    protected override string GroupSuffix => "user-invited-email";

    protected override async Task HandleAsync(UserInvitedIntegrationEvent message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var publisher = services.GetRequiredService<IEventPublisher>();

        var email = new EmailRequestedIntegrationEvent
        {
            CorrelationId = message.CorrelationId,
            To = message.Email,
            ToDisplayName = message.FullName,
            TemplateId = EmailTemplates.UserInvitation,
            // Keyed on the invitation, not the user: a resend rotates the invitation and must send again.
            IdempotencyKey = $"invitation:{message.InvitationId}:{message.EventId}",
            Model = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["FullName"] = message.FullName,
                ["Email"] = message.Email,
                ["InvitedByName"] = message.InvitedByName,
                ["ActionUrl"] = message.AcceptUrl,
                ["ExpiresAt"] = message.ExpiresAt.ToString("dddd d MMMM yyyy 'at' HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture),
            },
        };

        await publisher.PublishAsync(email, message.UserId.ToString(), cancellationToken);
    }
}
