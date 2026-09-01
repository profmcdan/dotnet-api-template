using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CleanArchTemplate.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
        context.Users
            .Where(u => u.Status == UserStatus.Active)
            .Where(u => EF.Property<List<string>>(u, "_roles").Contains(UserRoles.Administrator))
            .CountAsync(cancellationToken);

    public void Add(User user) => context.Users.Add(user);

    public void Remove(User user) => context.Users.Remove(user);
}
