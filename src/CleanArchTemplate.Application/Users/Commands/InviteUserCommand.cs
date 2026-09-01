using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Application.Options;
using CleanArchTemplate.Application.Users.Queries;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Application.Users.Commands;

public sealed record InviteUserCommand(string Email, string FullName, IReadOnlyList<string> Roles)
    : ICommand<UserResponse>, ITransactionalRequest;

internal sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(Domain.Users.Email.MaxLength);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(User.MaxNameLength);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role must be assigned.");
        RuleForEach(x => x.Roles)
            .Must(UserRoles.IsKnown)
            .WithMessage(role => $"'{role}' is not a recognised role.");
    }
}

/// <summary>
/// Creates the invited user and its invitation in one transaction. The outbound email is not
/// sent here: the save also writes an outbox row, which the outbox processor turns into a Kafka
/// message. That is what makes "user created" and "invitation email queued" atomic.
/// </summary>
internal sealed class InviteUserCommandHandler(
    IUserRepository users,
    IInvitationRepository invitations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    ICacheService cache,
    IOptions<InvitationOptions> invitationOptions) : ICommandHandler<InviteUserCommand, UserResponse>
{
    private readonly InvitationOptions _options = invitationOptions.Value;

    public async Task<Result<UserResponse>> HandleAsync(InviteUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } inviterId)
        {
            return Result.Failure<UserResponse>(UserErrors.InvalidCredentials);
        }

        var emailResult = Domain.Users.Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<UserResponse>(emailResult.Error);
        }

        var email = emailResult.Value;

        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.EmailAlreadyInUse(email.Value));
        }

        var inviter = await users.GetByIdAsync(inviterId, cancellationToken);
        if (inviter is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(inviterId));
        }

        var issued = UserInvitationService.Invite(
            email,
            request.FullName,
            request.Roles,
            inviter,
            _options.Lifetime,
            clock.UtcNow);

        if (issued.IsFailure)
        {
            return Result.Failure<UserResponse>(issued.Error);
        }

        users.Add(issued.Value.User);
        invitations.Add(issued.Value.Invitation);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByPrefixAsync(CacheKeys.UserListPrefix, cancellationToken);

        var created = issued.Value.User;
        return Result.Success(new UserResponse(
            created.Id,
            created.Email.Value,
            created.FullName,
            created.Status,
            created.Roles,
            created.CreatedAt,
            created.LastLoginAt,
            created.SuspensionReason));
    }
}
