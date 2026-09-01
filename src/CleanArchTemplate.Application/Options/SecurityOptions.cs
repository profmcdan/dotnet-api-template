using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Application.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Failed sign-ins tolerated per address inside <see cref="LockoutWindowMinutes"/>.</summary>
    [Range(1, 100)]
    public int MaxFailedLoginAttempts { get; set; } = 10;

    [Range(1, 1440)]
    public int LockoutWindowMinutes { get; set; } = 15;

    public TimeSpan LockoutWindow => TimeSpan.FromMinutes(LockoutWindowMinutes);
}
