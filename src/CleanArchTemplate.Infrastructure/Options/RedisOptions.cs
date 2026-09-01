using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Namespaces every key, so a shared Redis stays legible and flushable per service.</summary>
    public string InstanceName { get; set; } = "cleanarch";

    [Range(1, 86400)]
    public int DefaultTtlSeconds { get; set; } = 300;

    public TimeSpan DefaultTtl => TimeSpan.FromSeconds(DefaultTtlSeconds);
}
