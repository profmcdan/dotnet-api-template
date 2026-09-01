using CleanArchTemplate.Application;
using CleanArchTemplate.Infrastructure;
using CleanArchTemplate.Infrastructure.Configuration;
using CleanArchTemplate.Infrastructure.Options;
using CleanArchTemplate.Worker;
using CleanArchTemplate.Worker.Consumers;
using CleanArchTemplate.Worker.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

DotEnv.Load(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Host.UseSerilog((context, _, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service.name", "CleanArchTemplate.Worker")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// The outbox drain lives here rather than in the API, so publishing never competes with request
// latency and can be scaled independently of the request tier.
builder.Services.AddOutboxProcessor();

builder.Services.AddHostedService<TopicPreflightCheck>();

// Each consumer runs in its own group, so a slow email transport cannot stall user events.
builder.Services.AddHostedService<UserInvitedConsumer>();
builder.Services.AddHostedService<UserActivatedConsumer>();
builder.Services.AddHostedService<UserSuspendedConsumer>();
builder.Services.AddHostedService<UserReinstatedConsumer>();
builder.Services.AddHostedService<EmailDispatchConsumer>();

builder.Services.AddHostedService<ExpiredTokenCleanupJob>();

var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
var redis = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();

var health = builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

if (!string.IsNullOrWhiteSpace(database?.ConnectionString))
{
    health.AddNpgSql(database.ConnectionString, name: "postgres", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(redis?.ConnectionString))
{
    health.AddRedis(redis.ConnectionString, name: "redis", tags: ["ready"]);
}

var app = builder.Build();

// A minimal HTTP surface exists only so the orchestrator has something to probe.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

try
{
    await app.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
