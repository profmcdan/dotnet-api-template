using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Auth;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Invitations;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Auth.Commands;

/// <summary>
/// Completes the invite flow: consumes the single-use token, sets the first password and signs
/// the new user in, so the invitee is never bounced to a login form straight after choosing a
/// password they have not yet had a chance to save.
/// </summary>
public sealed record AcceptInvitationCommand(string Token, string Password, string? FullName)
    : ICommand<AuthTokensResponse>, ITransactionalRequest;

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        // Custom rather than Must + WithMessage: the policy explains *which* rule failed, and
        // that reason has to reach the caller instead of a generic minimum-length message.
        RuleFor(x => x.Password).Custom((password, context) =>
        {
            if (!PasswordPolicy.IsAcceptable(password, out var reason))
            {
                context.AddFailure(reason ?? "Password does not meet the minimum requirements.");
            }
        });
        RuleFor(x => x.FullName).MaximumLength(User.MaxNameLength).When(x => x.FullName is not null);
    }
}

internal sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitations,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    ICacheService cache,
    IClock clock) : ICommandHandler<AcceptInvitationCommand, AuthTokensResponse>
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsAcceptable(request.Password, out var reason))
        {
            return Result.Failure<AuthTokensResponse>(
                Error.Validation(AuthErrors.WeakPassword.Code, reason ?? AuthErrors.WeakPassword.Description));
        }

        var now = clock.UtcNow;
        var invitation = await invitations.GetByTokenHashAsync(InvitationToken.HashOf(request.Token), cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<AuthTokensResponse>(InvitationErrors.InvalidToken);
        }

        var accepted = invitation.Accept(now);
        if (accepted.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(accepted.Error);
        }

        var user = await users.GetByIdAsync(invitation.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthTokensResponse>(InvitationErrors.InvalidToken);
        }

        var activated = user.Activate(passwordHasher.Hash(request.Password), now);
        if (activated.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(activated.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            var renamed = user.Rename(request.FullName);
            if (renamed.IsFailure)
            {
                return Result.Failure<AuthTokensResponse>(renamed.Error);
            }
        }

        user.RecordLogin(now);

        var (refreshToken, rawRefreshToken) = RefreshToken.Issue(
            user.Id, tokenService.RefreshTokenLifetime, now, currentUser.IpAddress);

        refreshTokens.Add(refreshToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);

        return Result.Success(new AuthTokensResponse(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            LoginCommandHandler.ToResponse(user)));
    }
}
