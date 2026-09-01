using CleanArchTemplate.Application.Abstractions.Links;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Links;

/// <summary>
/// Builds outbound links from configuration only. Deriving them from the inbound request would
/// let a forged Host header rewrite the link inside an invitation email.
/// </summary>
internal sealed class AppLinkBuilder(IOptions<AppOptions> options) : IAppLinkBuilder
{
    private readonly AppOptions _options = options.Value;

    public string AcceptInvitationUrl(string token) =>
        $"{BaseUrl}{Normalise(_options.AcceptInvitationPath)}?token={Uri.EscapeDataString(token)}";

    public string SignInUrl() => $"{BaseUrl}{Normalise(_options.SignInPath)}";

    public string SupportUrl() => _options.SupportUrl;

    private string BaseUrl => _options.PublicBaseUrl.TrimEnd('/');

    private static string Normalise(string path) =>
        string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : $"/{path}";
}
