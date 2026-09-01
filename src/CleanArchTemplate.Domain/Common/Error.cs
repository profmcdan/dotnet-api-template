namespace CleanArchTemplate.Domain.Common;

/// <summary>
/// A machine-readable failure. <see cref="Code"/> is stable and safe to switch on;
/// <see cref="Description"/> is human-facing and may change.
/// </summary>
public record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error Unavailable(string code, string description) => new(code, description, ErrorType.Unavailable);

    public override string ToString() => $"{Code}: {Description}";
}
