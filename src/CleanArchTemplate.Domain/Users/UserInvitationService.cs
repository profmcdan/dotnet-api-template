using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;

namespace CleanArchTemplate.Domain.Users;

/// <summary>
/// The invited user and the invitation that lets them in. Both must be persisted in the same
/// transaction as the outbox row the invite event produces.
/// </summary>
public sealed record IssuedInvitation(User User, Invitation Invitation, InvitationToken Token);

/// <summary>
/// Coordinates the two aggregates that make up the invite flow. Lives in the domain because the
/// invariant it protects - a pending invitation always has exactly one invited user, and the raw
/// token exists only in the emitted event - is a domain rule, not an application concern.
/// </summary>
public static class UserInvitationService
{
    public static Result<IssuedInvitation> Invite(
        Email email,
        string fullName,
        IReadOnlyCollection<string> roles,
        User invitedBy,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        var userResult = User.Invite(email, fullName, roles);
        if (userResult.IsFailure)
        {
            return Result.Failure<IssuedInvitation>(userResult.Error);
        }

        var user = userResult.Value;
        var token = InvitationToken.Issue();
        var expiresAt = now.Add(lifetime);
        var invitation = Invitation.Issue(user.Id, email.Value, token, expiresAt, invitedBy.Id, now);

        user.RaiseInvited(invitation.Id, token.Raw, expiresAt, invitedBy.Id, invitedBy.FullName);

        return new IssuedInvitation(user, invitation, token);
    }

    public static Result<InvitationToken> Resend(
        User user,
        Invitation invitation,
        User resentBy,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        if (user.Status != UserStatus.Invited)
        {
            return Result.Failure<InvitationToken>(UserErrors.AlreadyActive);
        }

        var token = InvitationToken.Issue();
        var expiresAt = now.Add(lifetime);

        var refresh = invitation.Refresh(token, expiresAt, now);
        if (refresh.IsFailure)
        {
            return Result.Failure<InvitationToken>(refresh.Error);
        }

        user.RaiseInvitationResent(invitation.Id, token.Raw, expiresAt, resentBy.Id, resentBy.FullName);
        return token;
    }
}
