using CleanArchTemplate.Api.Contracts;
using CleanArchTemplate.Api.Extensions;
using CleanArchTemplate.Api.Security;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Users.Commands;
using CleanArchTemplate.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchTemplate.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
[Authorize]
public sealed class UsersController(ISender sender) : ControllerBase
{
    /// <summary>The caller's own profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetCurrentUserQuery(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Updates the caller's own profile.</summary>
    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new UpdateProfileCommand(request.FullName), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet]
    [Authorize(AuthorizationPolicies.RequireManager)]
    [ProducesResponseType<PagedResult<UserSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] ListUsersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.SendAsync(
            new ListUsersQuery(request.Page, request.PageSize, request.Search, request.Status, request.Role),
            cancellationToken);

        return result.ToActionResult(this);
    }

    [HttpGet("{userId:guid}")]
    [Authorize(AuthorizationPolicies.RequireManager)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetUserByIdQuery(userId), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Invites a new user. Returns immediately once the user and its invitation are committed -
    /// the email itself is delivered asynchronously through the outbox and the email worker.
    /// </summary>
    [HttpPost("invitations")]
    [Authorize(AuthorizationPolicies.RequireManager)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteAsync([FromBody] InviteUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.SendAsync(
            new InviteUserCommand(request.Email, request.FullName, request.Roles), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetByIdAsync), new { userId = result.Value.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>Rotates the invitation token and queues a fresh email. The old link stops working.</summary>
    [HttpPost("{userId:guid}/invitations/resend")]
    [Authorize(AuthorizationPolicies.RequireManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendInvitationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ResendInvitationCommand(userId), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Cancels a pending invitation and releases the email address for reuse.</summary>
    [HttpDelete("{userId:guid}/invitations")]
    [Authorize(AuthorizationPolicies.RequireManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeInvitationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new RevokeInvitationCommand(userId), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{userId:guid}/roles")]
    [Authorize(AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRolesAsync(Guid userId, [FromBody] ChangeRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new ChangeUserRolesCommand(userId, request.Roles), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Suspends an account and revokes all of its sessions.</summary>
    [HttpPost("{userId:guid}/suspend")]
    [Authorize(AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SuspendAsync(Guid userId, [FromBody] SuspendUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.SendAsync(new SuspendUserCommand(userId, request.Reason), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{userId:guid}/reinstate")]
    [Authorize(AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReinstateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ReinstateUserCommand(userId), cancellationToken);
        return result.ToActionResult(this);
    }
}
