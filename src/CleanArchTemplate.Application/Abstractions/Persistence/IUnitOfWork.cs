namespace CleanArchTemplate.Application.Abstractions.Persistence;

/// <summary>
/// The transactional boundary. One <see cref="SaveChangesAsync"/> writes the aggregate changes
/// and the outbox rows they produced in a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
