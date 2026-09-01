using System.Diagnostics;
using CleanArchTemplate.Application;
using CleanArchTemplate.Infrastructure;
using CleanArchTemplate.Infrastructure.Configuration;
using CleanArchTemplate.Infrastructure.Persistence;
using CleanArchTemplate.Migrator;
using CleanArchTemplate.Migrator.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

// The migrator is a run-to-completion job, not a service: it exits 0 on success and non-zero on
// failure, which is exactly what `depends_on: service_completed_successfully` needs in compose.
DotEnv.Load(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSerilog((_, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .Enrich.WithProperty("service.name", "CleanArchTemplate.Migrator")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<MigratorOptions>()
    .Bind(builder.Configuration.GetSection(MigratorOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddScoped<IMigrationStep, DatabaseMigrationStep>();
builder.Services.AddScoped<IMigrationStep, KafkaTopicSeedStep>();
builder.Services.AddScoped<IMigrationStep, AdministratorSeedStep>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var options = host.Services.GetRequiredService<IOptions<MigratorOptions>>().Value;

// A step name passed on the command line runs just that step, which is handy for re-seeding
// topics without touching the schema.
var only = args.FirstOrDefault(arg => !arg.StartsWith('-'));

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(options.StartupTimeoutSeconds, 30) + 300));

try
{
    await using var scope = host.Services.CreateAsyncScope();

    await WaitForDatabaseAsync(scope.ServiceProvider, options, logger, cancellation.Token);

    var steps = scope.ServiceProvider
        .GetServices<IMigrationStep>()
        .Where(step => only is null || string.Equals(step.Name, only, StringComparison.OrdinalIgnoreCase))
        .OrderBy(step => step.Order)
        .ToArray();

    if (steps.Length == 0)
    {
        throw new InvalidOperationException($"No migration step matched '{only}'.");
    }

    foreach (var step in steps)
    {
        MigratorLog.StepStarted(logger, step.Name);
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            await step.ExecuteAsync(cancellation.Token);
        }
        catch (Exception ex)
        {
            MigratorLog.StepFailed(logger, ex, step.Name);
            throw;
        }

        MigratorLog.StepFinished(logger, step.Name, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
    }

    MigratorLog.Completed(logger);
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Migrator failed");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Compose starts Postgres and the migrator at the same time; a health check gets us most of the
/// way, but the socket can still refuse connections for a moment after the container reports
/// healthy, so poll until it genuinely answers.
/// </summary>
static async Task WaitForDatabaseAsync(IServiceProvider services, MigratorOptions options, ILogger logger, CancellationToken cancellationToken)
{
    var context = services.GetRequiredService<AppDbContext>();
    var deadline = DateTimeOffset.UtcNow.AddSeconds(options.StartupTimeoutSeconds);
    var delay = TimeSpan.FromSeconds(1);

    MigratorLog.WaitingForDependency(logger, "postgres");

    while (true)
    {
        try
        {
            if (await context.Database.CanConnectAsync(cancellationToken))
            {
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw;
            }
        }

        if (DateTimeOffset.UtcNow >= deadline)
        {
            throw new TimeoutException($"The database did not become available within {options.StartupTimeoutSeconds}s.");
        }

        await Task.Delay(delay, cancellationToken);
        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
    }
}
