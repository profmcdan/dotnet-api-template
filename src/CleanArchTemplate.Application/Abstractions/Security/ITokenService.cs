using CleanArchTemplate.Domain.Users;

namespace CleanArchTemplate.Application.Abstractions.Security;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    TimeSpan RefreshTokenLifetime { get; }
}
