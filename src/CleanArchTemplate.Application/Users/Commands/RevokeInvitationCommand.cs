using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Commands;

/// <summary>
/// Cancels a pending invitation and removes the placeholder user, freeing the address to be
/// invited again. Only ever touches users that never activated.
/// </summary>
public sealed record RevokeInvitationCommand(Guid UserId) : ICommand, ITransactionalRequest;

internal sealed class RevokeInvitationCommandHandler(
    IUserRepository users,
    IInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    ICacheService cache) : ICommandHandler<RevokeInvitationCommand>
{
    public async Task<Result> HandleAsync(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId)
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.UserId));
        }

        if (user.Status != UserStatus.Invited)
        {
            return Result.Failure(UserErrors.AlreadyActive);
        }

        var invitation = await invitations.GetPendingForUserAsync(request.UserId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure(InvitationErrors.NotPending);
        }

        var revoked = invitation.Revoke(actorId, clock.UtcNow);
        if (revoked.IsFailure)
        {
            return revoked;
        }

        user.DeletedAt = clock.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
