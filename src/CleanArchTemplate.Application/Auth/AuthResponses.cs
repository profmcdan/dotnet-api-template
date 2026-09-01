using CleanArchTemplate.Application.Users.Queries;

namespace CleanArchTemplate.Application.Auth;

public sealed record AuthTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    UserResponse User)
{
    public string TokenType { get; init; } = "Bearer";
}
