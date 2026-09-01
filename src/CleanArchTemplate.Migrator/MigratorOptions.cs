using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Migrator;

public sealed class MigratorOptions
{
    public const string SectionName = "Migrator";

    /// <summary>
    /// Bootstrap administrator. Created only when the users table is empty, so re-running the
    /// migrator against a live database can never resurrect or reset a real account.
    /// </summary>
    [EmailAddress]
    public string? SeedAdminEmail { get; set; }

    public string? SeedAdminPassword { get; set; }

    public string SeedAdminName { get; set; } = "Administrator";

    /// <summary>Seconds to keep retrying the database and broker before giving up at startup.</summary>
    [Range(0, 600)]
    public int StartupTimeoutSeconds { get; set; } = 120;
}
