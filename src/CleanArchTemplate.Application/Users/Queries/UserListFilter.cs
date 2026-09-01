using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record UserListFilter(
    PageRequest Page,
    string? Search = null,
    UserStatus? Status = null,
    string? Role = null,
    UserSortBy SortBy = UserSortBy.CreatedAt,
    bool Descending = true);

public enum UserSortBy
{
    CreatedAt = 0,
    FullName = 1,
    Email = 2,
    LastLoginAt = 3,
}

public sealed record InvitationListFilter(
    PageRequest Page,
    InvitationStatus? Status = null,
    string? Search = null);
