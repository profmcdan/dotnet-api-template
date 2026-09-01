using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using CleanArchTemplate.Api.Extensions;
using CleanArchTemplate.Api.Middleware;
using CleanArchTemplate.Api.OpenApi;
using CleanArchTemplate.Api.Security;
using CleanArchTemplate.Application;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Infrastructure;
using CleanArchTemplate.Infrastructure.Configuration;
using CleanArchTemplate.Infrastructure.Options;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

// Local runs read .env; in compose the same keys arrive as real environment variables.
DotEnv.Load(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "CleanArchTemplate.Api";

builder.Configuration.AddEnvironmentVariables();
builder.Host.UseStructuredLogging(ServiceName);

// ---------------------------------------------------------------------------------------------
// Composition: the API knows about Application and Infrastructure; neither knows about the API.
// ---------------------------------------------------------------------------------------------
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Model-binding failures should look like every other 400 this API produces.
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressMapClientErrors = false);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddObservability(builder.Configuration, ServiceName);

// ---------------------------------------------------------------------------------------------
// Authentication and authorization
// ---------------------------------------------------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"The '{JwtOptions.SectionName}' configuration section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = jwt.ClockSkew,
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
        };

        // Makes suspension, role changes and password resets take effect immediately rather
        // than whenever the current access token happens to expire.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = SecurityStampValidator.ValidateAsync,
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.AddRateLimiter(RateLimitPolicies.Configure);

// ---------------------------------------------------------------------------------------------
// CORS - an explicit allow-list; never AllowAnyOrigin together with credentials.
// ---------------------------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }
}));

// ---------------------------------------------------------------------------------------------
// Health checks: /health/live answers "is the process up", /health/ready "can it serve traffic".
// ---------------------------------------------------------------------------------------------
var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
var redis = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
var kafka = builder.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>();

var health = builder.Services.AddHealthChecks();

if (!string.IsNullOrWhiteSpace(database?.ConnectionString))
{
    health.AddNpgSql(database.ConnectionString, name: "postgres", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(redis?.ConnectionString))
{
    health.AddRedis(redis.ConnectionString, name: "redis", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(kafka?.BootstrapServers))
{
    health.AddKafka(
        config => config.BootstrapServers = kafka.BootstrapServers,
        name: "kafka",
        tags: ["ready"]);
}

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

//#if (AllowGrpc)
builder.Services.AddGrpc(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddGrpcReflection();
//#endif

// Trust the reverse proxy for scheme and client IP; without this every caller looks like the proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
    exception is not null || context.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health")
            ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("CleanArchTemplate API")
        .WithTheme(ScalarTheme.Default));
}
else
{
    app.UseHsts();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting(RateLimitPolicies.Standard);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness must not depend on Postgres or Kafka, or a dependency blip triggers a pod restart.
    Predicate = _ => false,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
}).AllowAnonymous();

//#if (AllowGrpc)
app.MapGrpcService<CleanArchTemplate.Api.Grpc.UsersGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}
//#endif

app.Run();

/// <summary>Exposed so the integration test host can reference this entry point.</summary>
public partial class Program;
