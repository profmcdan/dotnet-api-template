using CleanArchTemplate.Application.Abstractions.Caching;
using CleanArchTemplate.Application.Abstractions.Dispatch;
using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Common;
using CleanArchTemplate.Domain.Users;
using FluentValidation;

namespace CleanArchTemplate.Application.Users.Commands;

public sealed record ChangeUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles) : ICommand;

internal sealed class ChangeUserRolesCommandValidator : AbstractValidator<ChangeUserRolesCommand>
{
    public ChangeUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles)
            .Must(UserRoles.IsKnown)
            .WithMessage(role => $"'{role}' is not a recognised role.");
    }
}

internal sealed class ChangeUserRolesCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<ChangeUserRolesCommand>
{
    public async Task<Result> HandleAsync(ChangeUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(request.UserId));
        }

        // Losing every administrator locks the whole tenant out of user management.
        var losesAdministrator = user.IsInRole(UserRoles.Administrator)
            && !request.Roles.Contains(UserRoles.Administrator, StringComparer.OrdinalIgnoreCase);

        if (losesAdministrator && await users.CountActiveAdministratorsAsync(cancellationToken) <= 1)
        {
            return Result.Failure(UserErrors.CannotDemoteLastAdministrator);
        }

        var changed = user.ChangeRoles(request.Roles);
        if (changed.IsFailure)
        {
            return changed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await UserCache.InvalidateAsync(cache, user.Id, cancellationToken);
        return Result.Success();
    }
}
