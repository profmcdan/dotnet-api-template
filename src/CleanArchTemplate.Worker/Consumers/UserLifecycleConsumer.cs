using CleanArchTemplate.Application.Abstractions.Links;
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

/// <summary>Sends the welcome mail once an invitee finishes activating.</summary>
internal sealed class UserActivatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger<UserActivatedConsumer> logger)
    : KafkaConsumerBase<UserActivatedIntegrationEvent>(scopeFactory, kafkaOptions, topics, logger)
{
    protected override string LogicalTopic => Topics.UserActivated;

    protected override string GroupSuffix => "user-activated-email";

    protected override async Task HandleAsync(UserActivatedIntegrationEvent message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var publisher = services.GetRequiredService<IEventPublisher>();
        var links = services.GetRequiredService<IAppLinkBuilder>();

        await publisher.PublishAsync(
            new EmailRequestedIntegrationEvent
            {
                CorrelationId = message.CorrelationId,
                To = message.Email,
                ToDisplayName = message.FullName,
                TemplateId = EmailTemplates.UserWelcome,
                IdempotencyKey = $"welcome:{message.UserId}",
                Model = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FullName"] = message.FullName,
                    ["Email"] = message.Email,
                    ["ActionUrl"] = links.SignInUrl(),
                },
            },
            message.UserId.ToString(),
            cancellationToken);
    }
}

/// <summary>Tells a user their account was suspended, and why.</summary>
internal sealed class UserSuspendedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger<UserSuspendedConsumer> logger)
    : KafkaConsumerBase<UserSuspendedIntegrationEvent>(scopeFactory, kafkaOptions, topics, logger)
{
    protected override string LogicalTopic => Topics.UserSuspended;

    protected override string GroupSuffix => "user-suspended-email";

    protected override async Task HandleAsync(UserSuspendedIntegrationEvent message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var publisher = services.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(
            new EmailRequestedIntegrationEvent
            {
                CorrelationId = message.CorrelationId,
                To = message.Email,
                TemplateId = EmailTemplates.AccountSuspended,
                IdempotencyKey = $"suspended:{message.EventId}",
                Model = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FullName"] = message.Email,
                    ["Email"] = message.Email,
                    ["Reason"] = message.Reason,
                },
            },
            message.UserId.ToString(),
            cancellationToken);
    }
}

/// <summary>Tells a user their account is usable again.</summary>
internal sealed class UserReinstatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ITopicResolver topics,
    ILogger<UserReinstatedConsumer> logger)
    : KafkaConsumerBase<UserReinstatedIntegrationEvent>(scopeFactory, kafkaOptions, topics, logger)
{
    protected override string LogicalTopic => Topics.UserReinstated;

    protected override string GroupSuffix => "user-reinstated-email";

    protected override async Task HandleAsync(UserReinstatedIntegrationEvent message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var publisher = services.GetRequiredService<IEventPublisher>();
        var links = services.GetRequiredService<IAppLinkBuilder>();

        await publisher.PublishAsync(
            new EmailRequestedIntegrationEvent
            {
                CorrelationId = message.CorrelationId,
                To = message.Email,
                TemplateId = EmailTemplates.AccountReinstated,
                IdempotencyKey = $"reinstated:{message.EventId}",
                Model = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FullName"] = message.Email,
                    ["Email"] = message.Email,
                    ["ActionUrl"] = links.SignInUrl(),
                },
            },
            message.UserId.ToString(),
            cancellationToken);
    }
}
