using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Domain.Invitations;
using Microsoft.EntityFrameworkCore;

namespace CleanArchTemplate.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(AppDbContext context) : IInvitationRepository
{
    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Invitations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<Invitation?> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Invitations
            .Where(i => i.UserId == userId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.LastSentAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(Invitation invitation) => context.Invitations.Add(invitation);
}
