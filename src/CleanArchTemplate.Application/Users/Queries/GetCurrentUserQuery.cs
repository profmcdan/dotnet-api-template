using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record GetCurrentUserQuery : IQuery<UserResponse>;

internal sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserReadRepository users)
    : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<UserResponse>(UserErrors.InvalidCredentials);
        }

        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound(userId))
            : Result.Success(user);
    }
}
