using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Commands;

public sealed record ReinstateUserCommand(Guid UserId) : ICommand;

internal sealed class ReinstateUserCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<ReinstateUserCommand>
{
    public async Task<Result> HandleAsync(ReinstateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.UserId));
        }

        var reinstated = user.Reinstate();
        if (reinstated.IsFailure)
        {
            return reinstated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
