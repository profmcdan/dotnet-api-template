using CleanArchTemplate.Domain.Common;

namespace CleanArchTemplate.Domain.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "The refresh token is invalid or has expired.");

    public static readonly Error RefreshTokenReuse =
        Error.Unauthorized("auth.refresh_token_reuse", "This session has been terminated. Please sign in again.");

    public static readonly Error WeakPassword =
        Error.Validation("auth.weak_password", "The password does not meet the minimum strength requirements.");

    public static readonly Error TooManyAttempts =
        Error.Failure("auth.too_many_attempts", "Too many sign-in attempts. Please try again later.");
}
