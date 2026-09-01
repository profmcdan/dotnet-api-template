using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Application.Users.Commands;

/// <summary>
/// Rotates the invitation secret and queues a fresh email. The previously mailed link stops
/// working immediately, so a resend doubles as a revoke of the old one.
/// </summary>
public sealed record ResendInvitationCommand(Guid UserId) : ICommand, ITransactionalRequest;

internal sealed class ResendInvitationCommandHandler(
    IUserRepository users,
    IInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<InvitationOptions> invitationOptions) : ICommandHandler<ResendInvitationCommand>
{
    public async Task<Result> HandleAsync(ResendInvitationCommand request, CancellationToken cancellationToken)
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

        var invitation = await invitations.GetPendingForUserAsync(request.UserId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure(InvitationErrors.NotPending);
        }

        var actor = await users.GetByIdAsync(actorId, cancellationToken);
        if (actor is null)
        {
            return Result.Failure(UserErrors.NotFound(actorId));
        }

        var resent = UserInvitationService.Resend(user, invitation, actor, invitationOptions.Value.Lifetime, clock.UtcNow);
        if (resent.IsFailure)
        {
            return Result.Failure(resent.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
