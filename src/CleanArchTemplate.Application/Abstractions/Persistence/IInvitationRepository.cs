using CleanArchTemplate.Domain.Invitations;

namespace CleanArchTemplate.Application.Abstractions.Persistence;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up by token hash - the raw token is never persisted or queried.</summary>
    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<Invitation?> GetPendingForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(Invitation invitation);
}
