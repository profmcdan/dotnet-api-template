using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Api.Contracts;

/// <summary>
/// Transport shapes for request bodies. Kept separate from the application commands so a public
/// API change and an internal refactor never force each other.
/// </summary>
public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken, bool AllSessions = false);

public sealed record AcceptInvitationRequest(string Token, string Password, string? FullName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record InviteUserRequest(string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record ChangeRolesRequest(IReadOnlyList<string> Roles);

public sealed record SuspendUserRequest(string Reason);

public sealed record UpdateProfileRequest(string FullName);

public sealed record ListUsersRequest(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    UserStatus? Status = null,
    string? Role = null);

public sealed record ListInvitationsRequest(
    int Page = 1,
    int PageSize = 25,
    InvitationStatus? Status = null,
    string? Search = null);
