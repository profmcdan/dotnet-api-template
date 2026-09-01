using CleanArchTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Migrator.Steps;

/// <summary>
/// Applies pending EF Core migrations.
/// <para>
/// Schema changes belong here rather than in the API's startup path: several API replicas
/// starting at once would otherwise race to migrate the same database.
/// </para>
/// </summary>
internal sealed class DatabaseMigrationStep(AppDbContext context, ILogger<DatabaseMigrationStep> logger) : IMigrationStep
{
    public string Name => "database-migrations";

    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            MigratorLog.NoPendingMigrations(logger);
            return;
        }

        MigratorLog.ApplyingMigrations(logger, pending.Length, string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
        MigratorLog.MigrationsApplied(logger, pending.Length);
    }
}
