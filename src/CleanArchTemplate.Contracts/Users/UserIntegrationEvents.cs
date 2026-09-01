using CleanArchTemplate.Contracts.Messaging;

namespace CleanArchTemplate.Contracts.Users;

/// <summary>
/// Published when a user is invited. <see cref="AcceptUrl"/> is a fully-formed link built by the
/// producer, so consumers never need to know how the front end routes invitations.
/// </summary>
[Topic(Topics.UserInvited)]
public sealed record UserInvitedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public required Guid InvitationId { get; init; }

    public required string AcceptUrl { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string InvitedByName { get; init; }

    /// <summary>True when this is a resend of an existing invitation rather than a first send.</summary>
    public bool IsResend { get; init; }
}

[Topic(Topics.UserActivated)]
public sealed record UserActivatedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }
}

[Topic(Topics.UserSuspended)]
public sealed record UserSuspendedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string Reason { get; init; }
}

[Topic(Topics.UserReinstated)]
public sealed record UserReinstatedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }
}

[Topic(Topics.UserRolesChanged)]
public sealed record UserRolesChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required IReadOnlyCollection<string> Roles { get; init; }
}

[Topic(Topics.InvitationRevoked)]
public sealed record InvitationRevokedIntegrationEvent : IntegrationEvent
{
    public required Guid InvitationId { get; init; }

    public required Guid UserId { get; init; }

    public required string Email { get; init; }
}
