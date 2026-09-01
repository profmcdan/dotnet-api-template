using CleanArchTemplate.Api.Contracts;
using CleanArchTemplate.Api.Extensions;
using CleanArchTemplate.Api.Security;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchTemplate.Api.Controllers;

/// <summary>
/// Read-only view over invitations. Issuing and revoking live on
/// <see cref="UsersController"/>, because both act on the user aggregate.
/// </summary>
[ApiController]
[Route("api/v1/invitations")]
[Produces("application/json")]
[Authorize(AuthorizationPolicies.RequireManager)]
public sealed class InvitationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<InvitationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] ListInvitationsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await sender.SendAsync(
            new ListInvitationsQuery(request.Page, request.PageSize, request.Status, request.Search),
            cancellationToken);

        return result.ToActionResult(this);
    }
}
