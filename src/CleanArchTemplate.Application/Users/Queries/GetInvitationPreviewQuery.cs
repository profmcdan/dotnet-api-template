using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;

namespace CleanArchTemplate.Application.Users.Queries;

/// <summary>
/// Lets the accept page show who the invitation is for before asking for a password.
/// Every rejection returns the same opaque error, so this cannot be used to enumerate tokens.
/// </summary>
public sealed record GetInvitationPreviewQuery(string Token) : IQuery<InvitationPreviewResponse>;

internal sealed class GetInvitationPreviewQueryHandler(
    IInvitationRepository invitations,
    IUserReadRepository users,
    IClock clock) : IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewResponse>
{
    public async Task<Result<InvitationPreviewResponse>> HandleAsync(GetInvitationPreviewQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<InvitationPreviewResponse>(InvitationErrors.InvalidToken);
        }

        var invitation = await invitations.GetByTokenHashAsync(InvitationToken.HashOf(request.Token), cancellationToken);

        if (invitation is null
            || invitation.Status != InvitationStatus.Pending
            || invitation.IsExpiredAt(clock.UtcNow))
        {
            return Result.Failure<InvitationPreviewResponse>(InvitationErrors.InvalidToken);
        }

        var user = await users.GetByIdAsync(invitation.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<InvitationPreviewResponse>(InvitationErrors.InvalidToken);
        }

        return Result.Success(new InvitationPreviewResponse(user.Email, user.FullName, invitation.ExpiresAt));
    }
}
