using System.ComponentModel.DataAnnotations;

namespace CleanArchTemplate.Infrastructure.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = "localhost:19092";

    /// <summary>
    /// Prepended to every topic name. Lets several environments or tenants share one broker
    /// without colliding; set per deployment.
    /// </summary>
    public string TopicPrefix { get; set; } = "cleanarch";

    [Required(AllowEmptyStrings = false)]
    public string ConsumerGroupId { get; set; } = "cleanarch-workers";

    public string SecurityProtocol { get; set; } = "Plaintext";

    public string? SaslMechanism { get; set; }

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }

    [Range(1, 100)]
    public int DefaultPartitions { get; set; } = 3;

    [Range(1, 5)]
    public short DefaultReplicationFactor { get; set; } = 1;

    [Range(1000, 300000)]
    public int SessionTimeoutMs { get; set; } = 45000;

    [Range(1, 100)]
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>Fail fast at startup when a required topic is missing rather than blocking on metadata.</summary>
    public bool ValidateTopicsOnStart { get; set; } = true;
}
