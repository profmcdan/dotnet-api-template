using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Users.Queries;

namespace CleanArchTemplate.Application.Abstractions.Persistence;

/// <summary>
/// The read side. Projects straight to DTOs with no change tracking, so queries never
/// materialise an aggregate they are not going to mutate.
/// </summary>
public interface IUserReadRepository
{
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<UserSummaryResponse>> ListAsync(UserListFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<InvitationResponse>> ListInvitationsAsync(InvitationListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Current security stamp, or null when the user is gone or soft-deleted. Used by the API to
    /// reject access tokens issued before a suspension, role change or password reset.
    /// </summary>
    Task<int?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default);
}
