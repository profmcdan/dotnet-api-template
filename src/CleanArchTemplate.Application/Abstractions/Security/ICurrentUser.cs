namespace CleanArchTemplate.Application.Abstractions.Security;

/// <summary>
/// Ambient identity for the request being handled. Resolves to an anonymous principal in
/// background workers, which is why <see cref="UserId"/> is nullable.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsAuthenticated { get; }

    string? IpAddress { get; }

    bool IsInRole(string role);
}
