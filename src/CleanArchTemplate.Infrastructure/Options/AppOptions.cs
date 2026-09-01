using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Infrastructure.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; } = "CleanArchTemplate";

    /// <summary>
    /// Public base URL of the front end. Links in emails are built from this and never from the
    /// inbound Host header, which an attacker controls.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    public string AcceptInvitationPath { get; set; } = "/accept-invitation";

    public string SignInPath { get; set; } = "/sign-in";

    public string SupportUrl { get; set; } = "mailto:support@example.com";
}
