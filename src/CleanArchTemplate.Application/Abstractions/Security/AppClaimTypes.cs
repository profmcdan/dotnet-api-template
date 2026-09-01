namespace CleanArchTemplate.Application.Abstractions.Security;

/// <summary>Custom claim names shared by the token issuer and every token consumer.</summary>
public static class AppClaimTypes
{
    /// <summary>
    /// The user's security stamp at issue time. Comparing it against the stored stamp is what
    /// lets a suspension or password change invalidate access tokens that have not yet expired.
    /// </summary>
    public const string SecurityStamp = "sstamp";
}
