using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record ListInvitationsQuery(
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize,
    InvitationStatus? Status = null,
    string? Search = null) : IQuery<PagedResult<InvitationResponse>>;

internal sealed class ListInvitationsQueryHandler(IUserReadRepository users)
    : IQueryHandler<ListInvitationsQuery, PagedResult<InvitationResponse>>
{
    public async Task<Result<PagedResult<InvitationResponse>>> HandleAsync(ListInvitationsQuery request, CancellationToken cancellationToken)
    {
        var filter = new InvitationListFilter(
            new PageRequest(request.Page, request.PageSize),
            request.Status,
            request.Search);

        return Result.Success(await users.ListInvitationsAsync(filter, cancellationToken));
    }
}
