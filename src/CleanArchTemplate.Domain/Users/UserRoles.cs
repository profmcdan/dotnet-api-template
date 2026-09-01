namespace CleanArchTemplate.Domain.Users;

/// <summary>
/// The role vocabulary. Roles are coarse-grained; fine-grained rules belong in
/// authorization policies that map onto these.
/// </summary>
public static class UserRoles
{
    public const string Administrator = "administrator";
    public const string Manager = "manager";
    public const string Member = "member";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Administrator, Manager, Member };

    public static bool IsKnown(string role) => All.Contains(role);
}
