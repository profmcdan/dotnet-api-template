using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace CleanArchTemplate.Api.Security;

/// <summary>
/// In-process rate limiting. It is per-instance, so treat it as defence in depth behind a shared
/// edge limiter rather than as the only control - N replicas allow N times these numbers.
/// </summary>
public static class RateLimitPolicies
{
    public const string Authentication = "auth";
    public const string Standard = "standard";

    public static void Configure(RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Credential endpoints are partitioned by IP and kept deliberately tight.
        options.AddPolicy(Authentication, context => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

        // Authenticated traffic is partitioned per user so one noisy client cannot starve the rest.
        options.AddPolicy(Standard, context => RateLimitPartition.GetTokenBucketLimiter(
            PartitionKey(context),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 200,
                TokensPerPeriod = 100,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { title = "Too many requests", status = 429, detail = "Slow down and try again shortly." },
                cancellationToken);
        };
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? $"user:{context.User.FindFirst("sub")?.Value ?? "unknown"}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
