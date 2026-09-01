using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CleanArchTemplate.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace CleanArchTemplate.Api.Security;

/// <summary>
/// Reads the caller's identity from the validated JWT. Claims are trusted only because the
/// bearer middleware has already verified the signature, issuer, audience and lifetime.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, CultureInfo.InvariantCulture, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.Email) ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <summary>
    /// Prefers the proxy-forwarded address. Only trustworthy because forwarded headers are
    /// processed by known proxies configured in <c>Program.cs</c>.
    /// </summary>
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
}
