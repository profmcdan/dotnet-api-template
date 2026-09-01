using CleanArchTemplate.Domain.Users;
using CleanArchTemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.Redpanda;

namespace CleanArchTemplate.Api.IntegrationTests.Fixtures;

/// <summary>
/// Runs the real API against real Postgres, Redis and Redpanda containers.
/// <para>
/// Nothing here is faked. Substituting an in-memory provider would silently stop testing the two
/// things most likely to break: the partial unique indexes, and whether the outbox interceptor
/// really writes its rows in the same transaction as the aggregate change.
/// </para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdministratorEmail = "admin@example.com";
    public const string AdministratorPassword = "bootstrap-admin-pass-1";
    public const string TopicPrefix = "itest";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("appdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine")
        .Build();

    private readonly RedpandaContainer _redpanda = new RedpandaBuilder("redpandadata/redpanda:v25.2.4")
        .Build();

    public string? SkipReason { get; private set; }

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public string KafkaBootstrapServers => _redpanda.GetBootstrapAddress();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _redpanda.StartAsync());
        }
        catch (Exception ex)
        {
            // No Docker on this machine or in this CI job - report it rather than failing loudly.
            SkipReason = $"Docker is not available for integration tests: {ex.Message}";
            return;
        }

        // Set before the host is first resolved. Program.cs reads Jwt and Database settings from
        // `builder.Configuration` inline, which happens before WebApplicationFactory's own
        // configuration callbacks run - environment variables are the only source available that
        // early, and they are the same mechanism compose uses.
        ApplyConfigurationEnvironment();

        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        await SeedAdministratorAsync(scope.ServiceProvider);
    }

    private void ApplyConfigurationEnvironment()
    {
        foreach (var (key, value) in Settings())
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private Dictionary<string, string> Settings() => new(StringComparer.Ordinal)
    {
        ["DATABASE__CONNECTIONSTRING"] = _postgres.GetConnectionString(),
        ["REDIS__CONNECTIONSTRING"] = _redis.GetConnectionString(),
        ["REDIS__INSTANCENAME"] = $"itest-{Guid.CreateVersion7():N}",
        ["KAFKA__BOOTSTRAPSERVERS"] = _redpanda.GetBootstrapAddress(),
        ["KAFKA__TOPICPREFIX"] = TopicPrefix,
        ["KAFKA__CONSUMERGROUPID"] = "itest-workers",
        ["JWT__ISSUER"] = "cleanarch-itest",
        ["JWT__AUDIENCE"] = "cleanarch-itest-clients",
        ["JWT__SIGNINGKEY"] = "integration-test-signing-key-that-is-long-enough-32",
        ["JWT__ACCESSTOKENMINUTES"] = "15",
        ["APP__PUBLICBASEURL"] = "https://app.test",
        ["EMAIL__SMTPHOST"] = "localhost",
        ["EMAIL__SMTPPORT"] = "1025",
        ["EMAIL__FROMADDRESS"] = "no-reply@app.test",
    };

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (SkipReason is null)
        {
            await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _redpanda.DisposeAsync().AsTask());
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
    }

    private static async Task SeedAdministratorAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<Application.Abstractions.Security.IPasswordHasher>();

        if (await context.Users.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var admin = User.CreateActive(
            Email.Create(AdministratorEmail).Value,
            "Root Admin",
            hasher.Hash(AdministratorPassword),
            [UserRoles.Administrator],
            DateTimeOffset.UtcNow).Value;

        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
