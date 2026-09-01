namespace CleanArchTemplate.Application.Abstractions.Links;

/// <summary>
/// Builds user-facing URLs from the configured public base address. Centralised so a link in an
/// email can never accidentally be built from an attacker-supplied Host header.
/// </summary>
public interface IAppLinkBuilder
{
    string AcceptInvitationUrl(string token);

    string SignInUrl();

    string SupportUrl();
}
