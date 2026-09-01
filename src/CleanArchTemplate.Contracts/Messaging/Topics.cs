namespace CleanArchTemplate.Contracts.Messaging;

/// <summary>
/// The logical topic catalogue. These are suffixes - the deployment-wide prefix
/// (<c>Kafka:TopicPrefix</c>) is prepended at runtime, which is what lets several
/// environments share one broker.
/// </summary>
public static class Topics
{
    public const string UserInvited = "user.invited";
    public const string UserActivated = "user.activated";
    public const string UserSuspended = "user.suspended";
    public const string UserReinstated = "user.reinstated";
    public const string UserRolesChanged = "user.roles-changed";
    public const string InvitationRevoked = "invitation.revoked";
    public const string EmailRequested = "email.requested";
    public const string EmailDeadLetter = "email.requested.dlq";

    /// <summary>Every topic the platform owns, with the partition/retention shape it needs.</summary>
    public static IReadOnlyList<TopicSpecification> All { get; } =
    [
        new(UserInvited, Partitions: 3),
        new(UserActivated, Partitions: 3),
        new(UserSuspended, Partitions: 3),
        new(UserReinstated, Partitions: 3),
        new(UserRolesChanged, Partitions: 3),
        new(InvitationRevoked, Partitions: 3),
        new(EmailRequested, Partitions: 6),
        // Dead letters are kept far longer than live traffic so failures can be replayed by hand.
        new(EmailDeadLetter, Partitions: 1, RetentionMs: 30L * 24 * 60 * 60 * 1000),
    ];
}

/// <summary>Declarative topic shape consumed by the migrator's topic-seeding step.</summary>
public sealed record TopicSpecification(
    string Name,
    int Partitions = 3,
    short ReplicationFactor = 1,
    long RetentionMs = 7L * 24 * 60 * 60 * 1000,
    string CleanupPolicy = "delete");
