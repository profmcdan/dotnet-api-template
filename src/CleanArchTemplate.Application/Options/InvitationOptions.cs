using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Application.Options;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>How long an invitation link stays usable. Short enough to limit exposure of a link left in a mailbox.</summary>
    [Range(1, 90)]
    public int LifetimeDays { get; set; } = 7;

    public TimeSpan Lifetime => TimeSpan.FromDays(LifetimeDays);
}
