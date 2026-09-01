using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Users.Queries;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    UserStatus Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string? SuspensionReason);

public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string FullName,
    UserStatus Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record InvitationResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    InvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSentAt,
    int SendCount,
    bool IsExpired);

/// <summary>What the accept-invitation page may know before any credentials are supplied.</summary>
public sealed record InvitationPreviewResponse(
    string Email,
    string FullName,
    DateTimeOffset ExpiresAt);
