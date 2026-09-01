using CleanArchTemplate.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace CleanArchTemplate.Api.Security;

/// <summary>
/// Named policies rather than <c>[Authorize(Roles = "...")]</c> strings scattered across
/// controllers: when the role model changes, it changes here only.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireAdministrator = "require:administrator";
    public const string RequireManager = "require:manager";
    public const string RequireMember = "require:member";

    public static void Configure(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(RequireAdministrator, policy =>
            policy.RequireAuthenticatedUser().RequireRole(UserRoles.Administrator));

        // Managers and administrators both run user administration.
        options.AddPolicy(RequireManager, policy =>
            policy.RequireAuthenticatedUser().RequireRole(UserRoles.Administrator, UserRoles.Manager));

        options.AddPolicy(RequireMember, policy =>
            policy.RequireAuthenticatedUser().RequireRole(UserRoles.Administrator, UserRoles.Manager, UserRoles.Member));

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }
}
