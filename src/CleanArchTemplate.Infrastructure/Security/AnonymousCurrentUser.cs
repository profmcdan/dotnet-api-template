using CleanArchTemplate.Application.Abstractions.Security;

namespace CleanArchTemplate.Infrastructure.Security;

/// <summary>
/// The ambient identity in background workers and the migrator, where no request principal
/// exists. Audit columns are left null rather than attributed to a fake system user.
/// </summary>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public string? Email => null;

    public IReadOnlyCollection<string> Roles => [];

    public bool IsAuthenticated => false;

    public string? IpAddress => null;

    public bool IsInRole(string role) => false;
}
