using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Invitations;

public static class InvitationErrors
{
    /// <summary>
    /// Deliberately opaque: a caller probing accept endpoints learns nothing about whether a
    /// token existed, was already used, or simply expired.
    /// </summary>
    public static readonly Error InvalidToken =
        Error.NotFound("invitation.invalid_token", "This invitation link is not valid or has expired.");

    public static Error NotFound(Guid id) =>
        Error.NotFound("invitation.not_found", $"No invitation with id '{id}' exists.");

    public static readonly Error AlreadyAccepted =
        Error.Conflict("invitation.already_accepted", "This invitation has already been accepted.");

    public static readonly Error Revoked =
        Error.Conflict("invitation.revoked", "This invitation has been revoked.");

    public static readonly Error Expired =
        Error.Conflict("invitation.expired", "This invitation has expired.");

    public static readonly Error NotPending =
        Error.Conflict("invitation.not_pending", "Only a pending invitation can be resent or revoked.");

    public static readonly Error ResendTooSoon =
        Error.Conflict("invitation.resend_too_soon", "An invitation email was sent recently; please wait before resending.");

    public static readonly Error AlreadyPending =
        Error.Conflict("invitation.already_pending", "An invitation for this address is already pending.");
}
