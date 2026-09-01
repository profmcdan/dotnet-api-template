using CleanArchTemplate.Contracts.Messaging;

namespace CleanArchTemplate.Contracts.Notifications;

/// <summary>
/// A request to deliver one templated email. Producers pick the template and supply its model;
/// rendering and transport are entirely the email worker's business.
/// </summary>
[Topic(Topics.EmailRequested)]
public sealed record EmailRequestedIntegrationEvent : IntegrationEvent
{
    public required string To { get; init; }

    public string? ToDisplayName { get; init; }

    public required string TemplateId { get; init; }

    /// <summary>Flat model handed to the template renderer. Keep it primitive - it crosses a wire.</summary>
    public required IReadOnlyDictionary<string, string> Model { get; init; }

    /// <summary>
    /// Collapses duplicate sends across retries and redeliveries. Two events with the same key
    /// deliver at most one email.
    /// </summary>
    public required string IdempotencyKey { get; init; }
}

/// <summary>The template catalogue. Ids match the file names under <c>EmailTemplates/</c>.</summary>
public static class EmailTemplates
{
    public const string UserInvitation = "user-invitation";
    public const string UserWelcome = "user-welcome";
    public const string AccountSuspended = "account-suspended";
    public const string AccountReinstated = "account-reinstated";
}
