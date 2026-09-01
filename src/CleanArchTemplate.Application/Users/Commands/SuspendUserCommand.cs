using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Users.Commands;

public sealed record SuspendUserCommand(Guid UserId, string Reason) : ICommand, ITransactionalRequest;

internal sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>
/// Suspends an account and tears down its live sessions. Bumping the security stamp is what
/// makes already-issued access tokens stop validating before their natural expiry.
/// </summary>
internal sealed class SuspendUserCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    ICacheService cache) : ICommandHandler<SuspendUserCommand>
{
    public async Task<Result> HandleAsync(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == request.UserId)
        {
            return Result.Failure(UserErrors.CannotSuspendSelf);
        }

        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.UserId));
        }

        if (user.IsInRole(UserRoles.Administrator) && await users.CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            return Result.Failure(UserErrors.CannotDemoteLastAdministrator);
        }

        var suspended = user.Suspend(request.Reason);
        if (suspended.IsFailure)
        {
            return suspended;
        }

        foreach (var token in await refreshTokens.GetActiveForUserAsync(user.Id, cancellationToken))
        {
            token.Revoke(clock.UtcNow, "user-suspended");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
