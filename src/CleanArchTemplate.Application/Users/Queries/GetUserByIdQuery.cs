using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserResponse>;

internal sealed class GetUserByIdQueryHandler(IUserReadRepository users, ICacheService cache)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<Result<UserResponse>> HandleAsync(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await cache.GetOrSetAsync(
            CacheKeys.User(request.UserId),
            ct => users.GetByIdAsync(request.UserId, ct),
            Ttl,
            cancellationToken);

        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound(request.UserId))
            : Result.Success(user);
    }
}
