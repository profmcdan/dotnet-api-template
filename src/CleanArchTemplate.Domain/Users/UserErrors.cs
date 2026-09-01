using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("user.not_found", $"No user with id '{id}' exists.");

    public static readonly Error NotFoundByEmail =
        Error.NotFound("user.not_found", "No user with that email address exists.");

    public static Error EmailAlreadyInUse(string email) =>
        Error.Conflict("user.email_in_use", $"'{email}' is already registered.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("user.invalid_credentials", "The email address or password is incorrect.");

    public static readonly Error NotActive =
        Error.Forbidden("user.not_active", "This account is not active.");

    public static readonly Error AlreadyActive =
        Error.Conflict("user.already_active", "This account has already been activated.");

    public static readonly Error AlreadySuspended =
        Error.Conflict("user.already_suspended", "This account is already suspended.");

    public static readonly Error NotSuspended =
        Error.Conflict("user.not_suspended", "This account is not suspended.");

    public static readonly Error NameRequired =
        Error.Validation("user.name_required", "Full name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("user.name_too_long", $"Full name must be {User.MaxNameLength} characters or fewer.");

    public static readonly Error RolesRequired =
        Error.Validation("user.roles_required", "At least one role must be assigned.");

    public static Error UnknownRole(string role) =>
        Error.Validation("user.unknown_role", $"'{role}' is not a recognised role.");

    public static readonly Error CannotSuspendSelf =
        Error.Forbidden("user.cannot_suspend_self", "You cannot suspend your own account.");

    public static readonly Error CannotDemoteLastAdministrator =
        Error.Conflict("user.last_administrator", "The last administrator cannot be demoted or suspended.");
}
