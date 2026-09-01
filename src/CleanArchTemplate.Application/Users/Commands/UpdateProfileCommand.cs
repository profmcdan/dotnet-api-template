using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Users.Commands;

/// <summary>Self-service profile edit. The caller can only ever change their own record.</summary>
public sealed record UpdateProfileCommand(string FullName) : ICommand;

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator() =>
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(User.MaxNameLength);
}

internal sealed class UpdateProfileCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICacheService cache) : ICommandHandler<UpdateProfileCommand>
{
    public async Task<Result> HandleAsync(UpdateProfileCommand request, CancellationToken cancellationToken)
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

        var renamed = user.Rename(request.FullName);
        if (renamed.IsFailure)
        {
            return renamed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
