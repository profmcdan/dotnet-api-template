using CleanArchTemplate.Api.Contracts;
using CleanArchTemplate.Api.Extensions;
using CleanArchTemplate.Api.Security;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Auth;
using CleanArchTemplate.Application.Auth.Commands;
using CleanArchTemplate.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CleanArchTemplate.Api.Controllers;

/// <summary>
/// Authentication endpoints. Like every controller here it depends on <see cref="ISender"/> only -
/// no DbContext, no repository, no direct database access of any kind.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Exchanges an email and password for an access and refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Rotates a refresh token. The presented token is invalidated whether or not this succeeds.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Revokes the presented refresh token, or every session for the caller.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new LogoutCommand(request.RefreshToken, request.AllSessions), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Shows who an invitation is for, so the accept page can be filled in before sign-up.</summary>
    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType<InvitationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewInvitationAsync(string token, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetInvitationPreviewQuery(token), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Consumes an invitation, sets the first password and signs the new user in.</summary>
    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptInvitationAsync([FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(
            new AcceptInvitationCommand(request.Token, request.Password, request.FullName), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Changes the caller's own password and signs out every other session.</summary>
    [HttpPost("password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), cancellationToken);

        return result.ToActionResult(this);
    }
}
