using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace CleanArchTemplate.Api.Middleware;

/// <summary>
/// Threads a correlation id through logs and the response. An inbound id is honoured so a trace
/// spans the caller and this service; otherwise one is minted.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        // Never echo unbounded caller input straight into logs and response headers.
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > MaxLength)
        {
            correlationId = Guid.CreateVersion7().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
