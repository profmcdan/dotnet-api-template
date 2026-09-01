using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CleanArchTemplate.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> at design time. Reads the connection string from
/// <c>ConnectionStrings__Default</c> / <c>DATABASE__CONNECTIONSTRING</c> so that adding a
/// migration never needs the full application host to start.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=appdb;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE__CONNECTIONSTRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new AppDbContext(options);
    }
}
