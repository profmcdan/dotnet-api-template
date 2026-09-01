using System.Globalization;
using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CleanArchTemplate.Api.Security;

/// <summary>
/// Rejects access tokens issued before a suspension, role change or password reset.
/// <para>
/// The stamp is cached briefly, so the common path costs a Redis GET rather than a database
/// round-trip; the cache is evicted by the commands that bump the stamp, which is why a short TTL
/// is enough. Without this check a 15-minute access token would outlive its own revocation.
/// </para>
/// </summary>
internal static class SecurityStampValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var claim = context.Principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value;
        var subject = context.Principal?.FindFirst("sub")?.Value
            ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(claim, CultureInfo.InvariantCulture, out var tokenStamp)
            || !Guid.TryParse(subject, CultureInfo.InvariantCulture, out var userId))
        {
            context.Fail("The token is missing the identity claims required to validate it.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<ICacheService>();
        var users = services.GetRequiredService<IUserReadRepository>();

        var currentStamp = await cache.GetOrSetAsync(
            CacheKeys.SecurityStamp(userId),
            async ct => await users.GetSecurityStampAsync(userId, ct) ?? -1,
            CacheTtl,
            context.HttpContext.RequestAborted);

        if (currentStamp != tokenStamp)
        {
            context.Fail("This session is no longer valid. Please sign in again.");
        }
    }
}
