using System.Globalization;
using CleanArchTemplate.Api.Security;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Users.Queries;
using CleanArchTemplate.Domain.Common;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace CleanArchTemplate.Api.Grpc;

/// <summary>
/// gRPC facade over the same query handlers the REST controllers use. It adds a transport, not a
/// second implementation, so authorization and validation behave identically on both surfaces.
/// </summary>
[Authorize(AuthorizationPolicies.RequireManager)]
public sealed class UsersGrpcService(ISender sender) : Users.UsersBase
{
    public override async Task<UserReply> GetUser(GetUserRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.UserId, CultureInfo.InvariantCulture, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id must be a UUID."));
        }

        var result = await sender.SendAsync(new GetUserByIdQuery(userId), context.CancellationToken);

        if (result.IsFailure)
        {
            throw ToRpcException(result.Error);
        }

        var user = result.Value;

        return new UserReply
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            FullName = user.FullName,
            Status = user.Status.ToString(),
            Roles = { user.Roles },
            CreatedAt = Timestamp.FromDateTimeOffset(user.CreatedAt),
            LastLoginAt = user.LastLoginAt is { } lastLogin ? Timestamp.FromDateTimeOffset(lastLogin) : null,
        };
    }

    public override async Task<ListUsersReply> ListUsers(ListUsersRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var query = new ListUsersQuery(
            request.Page <= 0 ? 1 : request.Page,
            request.PageSize <= 0 ? 25 : request.PageSize,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search,
            Status: null,
            Role: string.IsNullOrWhiteSpace(request.Role) ? null : request.Role);

        var result = await sender.SendAsync(query, context.CancellationToken);

        if (result.IsFailure)
        {
            throw ToRpcException(result.Error);
        }

        var page = result.Value;
        var reply = new ListUsersReply
        {
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
        };

        reply.Items.AddRange(page.Items.Select(user => new UserReply
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            FullName = user.FullName,
            Status = user.Status.ToString(),
            Roles = { user.Roles },
            CreatedAt = Timestamp.FromDateTimeOffset(user.CreatedAt),
            LastLoginAt = user.LastLoginAt is { } lastLogin ? Timestamp.FromDateTimeOffset(lastLogin) : null,
        }));

        return reply;
    }

    /// <summary>Mirrors the REST status mapping so both transports report the same failure the same way.</summary>
    private static RpcException ToRpcException(Error error) => new(new Status(
        error.Type switch
        {
            ErrorType.Validation => StatusCode.InvalidArgument,
            ErrorType.NotFound => StatusCode.NotFound,
            ErrorType.Conflict => StatusCode.AlreadyExists,
            ErrorType.Unauthorized => StatusCode.Unauthenticated,
            ErrorType.Forbidden => StatusCode.PermissionDenied,
            ErrorType.Unavailable => StatusCode.Unavailable,
            _ => StatusCode.Internal,
        },
        error.Description));
}
