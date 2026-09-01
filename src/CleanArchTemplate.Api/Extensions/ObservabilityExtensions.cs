using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace CleanArchTemplate.Api.Extensions;

/// <summary>
/// Structured logging plus traces and metrics. The OTLP exporter is wired only when an endpoint
/// is configured, so a developer running <c>dotnet run</c> needs no collector.
/// </summary>
public static class ObservabilityExtensions
{
    public static IHostBuilder UseStructuredLogging(this IHostBuilder host, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.UseSerilog((context, _, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("service.name", serviceName)
            .Enrich.WithProperty("service.environment", context.HostingEnvironment.EnvironmentName)
            // Compact rendering in containers; human-readable when running locally.
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));
    }

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var otlpEndpoint = configuration["Otlp:Endpoint"];

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    // Health probes would otherwise dominate the trace volume.
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder
                .WithTracing(tracing => tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
                .WithMetrics(metrics => metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));
        }

        // Correlate logs with traces without every call site having to pass the ids around.
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        return services;
    }
}
