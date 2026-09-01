using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Auth.Commands;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword)
    : ICommand, ITransactionalRequest;

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

/// <summary>
/// Changes the caller's own password and revokes every other session, which is the only way a
/// user who suspects compromise can actually evict the intruder.
/// </summary>
internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    ICacheService cache,
    IClock clock) : ICommandHandler<ChangePasswordCommand>
{
    public async Task<Result> HandleAsync(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(userId));
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        if (!PasswordPolicy.IsAcceptable(request.NewPassword, out var reason))
        {
            return Result.Failure(Error.Validation(AuthErrors.WeakPassword.Code, reason ?? AuthErrors.WeakPassword.Description));
        }

        var now = clock.UtcNow;

        var changed = user.ChangePassword(passwordHasher.Hash(request.NewPassword), now);
        if (changed.IsFailure)
        {
            return changed;
        }

        foreach (var token in await refreshTokens.GetActiveForUserAsync(user.Id, cancellationToken))
        {
            token.Revoke(now, "password-changed");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
