using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Security;
using CleanArchTemplate.Application.Abstractions.Time;
using CleanArchTemplate.Application.Common;
using CleanArchTemplate.Domain.Users;
using CleanArchTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Migrator.Steps;

/// <summary>
/// Creates the first administrator so a fresh deployment is reachable - somebody has to be able
/// to send the first invitation.
/// <para>
/// Runs only when the users table is completely empty. That single condition is what makes it
/// safe on every subsequent deploy: it can never overwrite a real account or reset a password.
/// </para>
/// </summary>
internal sealed class AdministratorSeedStep(
    AppDbContext context,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IClock clock,
    IOptions<MigratorOptions> options,
    ILogger<AdministratorSeedStep> logger) : IMigrationStep
{
    private readonly MigratorOptions _options = options.Value;

    public string Name => "administrator-seed";

    public int Order => 30;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SeedAdminEmail) || string.IsNullOrWhiteSpace(_options.SeedAdminPassword))
        {
            MigratorLog.SeedSkipped(logger, "no seed administrator is configured");
            return;
        }

        if (await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            MigratorLog.SeedSkipped(logger, "the users table is not empty");
            return;
        }

        if (!PasswordPolicy.IsAcceptable(_options.SeedAdminPassword, out var reason))
        {
            throw new InvalidOperationException($"The seed administrator password is not acceptable: {reason}");
        }

        var email = Email.Create(_options.SeedAdminEmail);
        if (email.IsFailure)
        {
            throw new InvalidOperationException($"The seed administrator email is invalid: {email.Error.Description}");
        }

        var user = User.CreateActive(
            email.Value,
            _options.SeedAdminName,
            passwordHasher.Hash(_options.SeedAdminPassword),
            [UserRoles.Administrator],
            clock.UtcNow);

        if (user.IsFailure)
        {
            throw new InvalidOperationException($"The seed administrator could not be created: {user.Error.Description}");
        }

        users.Add(user.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        MigratorLog.SeededAdministrator(logger, email.Value.Value);
    }
}
