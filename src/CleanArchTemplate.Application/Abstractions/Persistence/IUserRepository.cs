using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Abstractions.Persistence;

/// <summary>Write-side access to the <see cref="User"/> aggregate. Returns tracked entities.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Used to stop the last administrator being suspended or demoted.</summary>
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default);

    void Add(User user);

    void Remove(User user);
}
