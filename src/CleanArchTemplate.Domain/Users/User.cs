using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users.Events;

namespace CleanArchTemplate.Domain.Users;

public sealed class User : AggregateRoot<Guid>, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 200;

    private readonly List<string> _roles = [];

    private User(Guid id, Email email, string fullName, UserStatus status, IEnumerable<string> roles)
        : base(id)
    {
        Email = email;
        FullName = fullName;
        Status = status;
        _roles.AddRange(roles);
    }

    // EF Core materialisation.
    private User()
    {
        Email = null!;
        FullName = null!;
    }

    public Email Email { get; private set; }

    public string FullName { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary>Null while the user is still <see cref="UserStatus.Invited"/>.</summary>
    public string? PasswordHash { get; private set; }

    public DateTimeOffset? PasswordChangedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public string? SuspensionReason { get; private set; }

    /// <summary>Bumped on password change, suspension and role change so live access tokens stop validating.</summary>
    public int SecurityStamp { get; private set; }

    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public bool CanAuthenticate => Status == UserStatus.Active && PasswordHash is not null && DeletedAt is null;

    public bool IsInRole(string role) => _roles.Contains(role, StringComparer.Ordinal);

    /// <summary>
    /// Creates a user in the <see cref="UserStatus.Invited"/> state. Credentials are set later,
    /// when the invitee accepts. Prefer <see cref="UserInvitationService"/> so the invitation
    /// aggregate and the outbound event stay in step.
    /// </summary>
    public static Result<User> Invite(Email email, string fullName, IReadOnlyCollection<string> roles)
    {
        var nameCheck = ValidateName(fullName);
        if (nameCheck.IsFailure)
        {
            return Result.Failure<User>(nameCheck.Error);
        }

        var roleCheck = ValidateRoles(roles);
        if (roleCheck.IsFailure)
        {
            return Result.Failure<User>(roleCheck.Error);
        }

        return new User(Guid.CreateVersion7(), email, fullName.Trim(), UserStatus.Invited, roleCheck.Value);
    }

    /// <summary>Creates an already-active user. Used by the seeder for the bootstrap administrator.</summary>
    public static Result<User> CreateActive(Email email, string fullName, string passwordHash, IReadOnlyCollection<string> roles, DateTimeOffset now)
    {
        var result = Invite(email, fullName, roles);
        if (result.IsFailure)
        {
            return result;
        }

        var user = result.Value;
        user.Status = UserStatus.Active;
        user.PasswordHash = passwordHash;
        user.PasswordChangedAt = now;
        return user;
    }

    public Result Activate(string passwordHash, DateTimeOffset now)
    {
        if (Status == UserStatus.Active)
        {
            return Result.Failure(UserErrors.AlreadyActive);
        }

        if (Status == UserStatus.Suspended)
        {
            return Result.Failure(UserErrors.NotActive);
        }

        Status = UserStatus.Active;
        PasswordHash = passwordHash;
        PasswordChangedAt = now;
        SecurityStamp++;
        Raise(new UserActivatedDomainEvent(Id, Email.Value, FullName));
        return Result.Success();
    }

    public Result ChangePassword(string passwordHash, DateTimeOffset now)
    {
        if (Status != UserStatus.Active)
        {
            return Result.Failure(UserErrors.NotActive);
        }

        PasswordHash = passwordHash;
        PasswordChangedAt = now;
        SecurityStamp++;
        return Result.Success();
    }

    public Result Rename(string fullName)
    {
        var nameCheck = ValidateName(fullName);
        if (nameCheck.IsFailure)
        {
            return nameCheck;
        }

        FullName = fullName.Trim();
        return Result.Success();
    }

    public Result ChangeRoles(IReadOnlyCollection<string> roles)
    {
        var roleCheck = ValidateRoles(roles);
        if (roleCheck.IsFailure)
        {
            return Result.Failure(roleCheck.Error);
        }

        if (_roles.Count == roleCheck.Value.Count && roleCheck.Value.All(IsInRole))
        {
            return Result.Success();
        }

        _roles.Clear();
        _roles.AddRange(roleCheck.Value);
        SecurityStamp++;
        Raise(new UserRolesChangedDomainEvent(Id, Email.Value, Roles));
        return Result.Success();
    }

    public Result Suspend(string reason)
    {
        if (Status == UserStatus.Suspended)
        {
            return Result.Failure(UserErrors.AlreadySuspended);
        }

        Status = UserStatus.Suspended;
        SuspensionReason = string.IsNullOrWhiteSpace(reason) ? "No reason supplied." : reason.Trim();
        SecurityStamp++;
        Raise(new UserSuspendedDomainEvent(Id, Email.Value, SuspensionReason));
        return Result.Success();
    }

    public Result Reinstate()
    {
        if (Status != UserStatus.Suspended)
        {
            return Result.Failure(UserErrors.NotSuspended);
        }

        // An invitee that was suspended before accepting returns to the invited state.
        Status = PasswordHash is null ? UserStatus.Invited : UserStatus.Active;
        SuspensionReason = null;
        SecurityStamp++;
        Raise(new UserReinstatedDomainEvent(Id, Email.Value));
        return Result.Success();
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    internal void RaiseInvited(Guid invitationId, string rawToken, DateTimeOffset expiresAt, Guid invitedByUserId, string invitedByName) =>
        Raise(new UserInvitedDomainEvent(Id, Email.Value, FullName, invitationId, rawToken, expiresAt, invitedByUserId, invitedByName));

    internal void RaiseInvitationResent(Guid invitationId, string rawToken, DateTimeOffset expiresAt, Guid resentByUserId, string resentByName) =>
        Raise(new UserInvitationResentDomainEvent(Id, Email.Value, FullName, invitationId, rawToken, expiresAt, resentByUserId, resentByName));

    private static Result ValidateName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(UserErrors.NameRequired);
        }

        return fullName.Trim().Length > MaxNameLength
            ? Result.Failure(UserErrors.NameTooLong)
            : Result.Success();
    }

    private static Result<List<string>> ValidateRoles(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0)
        {
            return Result.Failure<List<string>>(UserErrors.RolesRequired);
        }

        var normalised = new List<string>();
        foreach (var role in roles)
        {
            var value = role?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!UserRoles.IsKnown(value))
            {
                return Result.Failure<List<string>>(UserErrors.UnknownRole(role ?? string.Empty));
            }

            if (!normalised.Contains(value, StringComparer.Ordinal))
            {
                normalised.Add(value);
            }
        }

        return normalised;
    }
}
