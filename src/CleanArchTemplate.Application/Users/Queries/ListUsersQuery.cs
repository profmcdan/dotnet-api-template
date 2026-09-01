using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record ListUsersQuery(
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize,
    string? Search = null,
    UserStatus? Status = null,
    string? Role = null,
    UserSortBy SortBy = UserSortBy.CreatedAt,
    bool Descending = true) : IQuery<PagedResult<UserSummaryResponse>>;

internal sealed class ListUsersQueryHandler(IUserReadRepository users)
    : IQueryHandler<ListUsersQuery, PagedResult<UserSummaryResponse>>
{
    public async Task<Result<PagedResult<UserSummaryResponse>>> HandleAsync(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var filter = new UserListFilter(
            new PageRequest(request.Page, request.PageSize),
            request.Search,
            request.Status,
            request.Role,
            request.SortBy,
            request.Descending);

        return Result.Success(await users.ListAsync(filter, cancellationToken));
    }
}
