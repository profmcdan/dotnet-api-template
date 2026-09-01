using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations.Events;

namespace CleanArchTemplate.Domain.Invitations;

public sealed class Invitation : AggregateRoot<Guid>, IAuditable
{
    /// <summary>Guards the resend endpoint against being used as a mail amplifier.</summary>
    public static readonly TimeSpan MinimumResendInterval = TimeSpan.FromMinutes(2);

    private Invitation(Guid id, Guid userId, string email, string tokenHash, DateTimeOffset expiresAt, Guid invitedByUserId, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        Email = email;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        InvitedByUserId = invitedByUserId;
        Status = InvitationStatus.Pending;
        LastSentAt = now;
    }

    // EF Core materialisation.
    private Invitation()
    {
        Email = null!;
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }

    public string Email { get; private set; }

    /// <summary>SHA-256 of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public InvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public DateTimeOffset LastSentAt { get; private set; }

    public int SendCount { get; private set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    internal static Invitation Issue(Guid userId, string email, InvitationToken token, DateTimeOffset expiresAt, Guid invitedByUserId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, email, token.Hash, expiresAt, invitedByUserId, now);

    /// <summary>
    /// Rotates the secret and extends the window. The previous token stops working immediately,
    /// so a resend also acts as a revoke of the link already in the mailbox.
    /// </summary>
    internal Result Refresh(InvitationToken token, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return Result.Failure(Status == InvitationStatus.Accepted
                ? InvitationErrors.AlreadyAccepted
                : InvitationErrors.Revoked);
        }

        if (now - LastSentAt < MinimumResendInterval)
        {
            return Result.Failure(InvitationErrors.ResendTooSoon);
        }

        TokenHash = token.Hash;
        ExpiresAt = expiresAt;
        LastSentAt = now;
        SendCount++;
        return Result.Success();
    }

    public Result Accept(DateTimeOffset now)
    {
        if (Status == InvitationStatus.Accepted)
        {
            return Result.Failure(InvitationErrors.AlreadyAccepted);
        }

        if (Status == InvitationStatus.Revoked)
        {
            return Result.Failure(InvitationErrors.Revoked);
        }

        if (IsExpiredAt(now))
        {
            return Result.Failure(InvitationErrors.Expired);
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        return Result.Success();
    }

    public Result Revoke(Guid revokedByUserId, DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return Result.Failure(InvitationErrors.NotPending);
        }

        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        RevokedByUserId = revokedByUserId;
        Raise(new InvitationRevokedDomainEvent(Id, UserId, Email, revokedByUserId));
        return Result.Success();
    }
}
