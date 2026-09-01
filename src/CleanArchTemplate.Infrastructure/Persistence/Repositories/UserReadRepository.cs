using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Users.Queries;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CleanArchTemplate.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read side. Every query is <c>AsNoTracking</c> and projects into a DTO before materialising,
/// so a list screen never drags aggregates - or their domain events - into the change tracker.
/// </summary>
internal sealed class UserReadRepository(AppDbContext context) : IUserReadRepository
{
    public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserResponse(
                u.Id,
                u.Email.Value,
                u.FullName,
                u.Status,
                EF.Property<List<string>>(u, "_roles"),
                u.CreatedAt,
                u.LastLoginAt,
                u.SuspensionReason))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Suspended users still return a stamp; only deletion removes them from the filtered set.
        var stamps = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == id && u.Status == UserStatus.Active)
            .Select(u => (int?)u.SecurityStamp)
            .FirstOrDefaultAsync(cancellationToken);

        return stamps;
    }

    public async Task<PagedResult<UserSummaryResponse>> ListAsync(UserListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, term) || EF.Functions.ILike(u.Email.Value, term));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var role = filter.Role.Trim().ToLowerInvariant();
            query = query.Where(u => EF.Property<List<string>>(u, "_roles").Contains(role));
        }

        var total = await query.LongCountAsync(cancellationToken);

        var ordered = (filter.SortBy, filter.Descending) switch
        {
            (UserSortBy.FullName, false) => query.OrderBy(u => u.FullName),
            (UserSortBy.FullName, true) => query.OrderByDescending(u => u.FullName),
            (UserSortBy.Email, false) => query.OrderBy(u => u.Email.Value),
            (UserSortBy.Email, true) => query.OrderByDescending(u => u.Email.Value),
            (UserSortBy.LastLoginAt, false) => query.OrderBy(u => u.LastLoginAt),
            (UserSortBy.LastLoginAt, true) => query.OrderByDescending(u => u.LastLoginAt),
            (_, false) => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt),
        };

        // Id is the tiebreaker: without it, equal sort keys can repeat or skip rows across pages.
        var items = await ordered
            .ThenBy(u => u.Id)
            .Skip(filter.Page.Skip)
            .Take(filter.Page.PageSize)
            .Select(u => new UserSummaryResponse(
                u.Id,
                u.Email.Value,
                u.FullName,
                u.Status,
                EF.Property<List<string>>(u, "_roles"),
                u.CreatedAt,
                u.LastLoginAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserSummaryResponse>(items, filter.Page.Page, filter.Page.PageSize, total);
    }

    public async Task<PagedResult<InvitationResponse>> ListInvitationsAsync(InvitationListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var now = DateTimeOffset.UtcNow;

        var query = from invitation in context.Invitations.AsNoTracking()
                    join user in context.Users.AsNoTracking() on invitation.UserId equals user.Id
                    select new { invitation, user };

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.invitation.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.invitation.Email, term) || EF.Functions.ILike(x.user.FullName, term));
        }

        var total = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.invitation.CreatedAt)
            .ThenBy(x => x.invitation.Id)
            .Skip(filter.Page.Skip)
            .Take(filter.Page.PageSize)
            .Select(x => new InvitationResponse(
                x.invitation.Id,
                x.invitation.UserId,
                x.invitation.Email,
                x.user.FullName,
                x.invitation.Status,
                x.invitation.ExpiresAt,
                x.invitation.CreatedAt,
                x.invitation.LastSentAt,
                x.invitation.SendCount,
                x.invitation.ExpiresAt <= now))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvitationResponse>(items, filter.Page.Page, filter.Page.PageSize, total);
    }
}
