namespace CleanArchTemplate.Migrator.Steps;

/// <summary>
/// One bootstrap action. Steps run in <see cref="Order"/> and every one must be idempotent -
/// the migrator runs on every deploy, not just the first.
/// </summary>
public interface IMigrationStep
{
    string Name { get; }

    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
