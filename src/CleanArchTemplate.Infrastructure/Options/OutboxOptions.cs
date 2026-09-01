using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Infrastructure.Options;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; set; } = 5;

    [Range(1, 20)]
    public int MaxAttempts { get; set; } = 8;

    /// <summary>How long successfully published rows are kept before the cleanup pass removes them.</summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; } = 7;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
}
