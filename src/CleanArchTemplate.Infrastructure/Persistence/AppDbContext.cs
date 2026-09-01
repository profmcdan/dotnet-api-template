using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using CleanArchTemplate.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CleanArchTemplate.Infrastructure.Persistence;

/// <summary>
/// The single write-side context and the implementation of <see cref="IUnitOfWork"/>.
/// Interceptors registered alongside it stamp audit columns and turn domain events into outbox
/// rows, so one <c>SaveChanges</c> commits the aggregate change and its outbound messages together.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Already inside a transaction (nested handler, or a test fixture) - join it rather than nest.
        if (Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Soft-deleted users are invisible to every query unless a repository opts out explicitly.
        modelBuilder.Entity<User>().HasQueryFilter(u => u.DeletedAt == null);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store every timestamp as timestamptz so nothing depends on the server's local zone.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        configurationBuilder.Properties<string>().AreUnicode().HaveMaxLength(1024);

        base.ConfigureConventions(configurationBuilder);
    }
}
