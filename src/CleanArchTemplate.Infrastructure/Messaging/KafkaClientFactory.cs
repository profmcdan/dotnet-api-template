using Confluent.Kafka;
using CleanArchTemplate.Infrastructure.Options;

namespace CleanArchTemplate.Infrastructure.Messaging;

/// <summary>
/// Builds the shared Kafka client configuration. Kept in one place so producers, consumers,
/// the admin client and health checks cannot drift apart on security settings.
/// </summary>
internal static class KafkaClientFactory
{
    public static ClientConfig CreateClientConfig(KafkaOptions options)
    {
        var config = new ClientConfig
        {
            BootstrapServers = options.BootstrapServers,
        };

        if (Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, ignoreCase: true, out var protocol))
        {
            config.SecurityProtocol = protocol;
        }

        if (!string.IsNullOrWhiteSpace(options.SaslMechanism)
            && Enum.TryParse<SaslMechanism>(options.SaslMechanism, ignoreCase: true, out var mechanism))
        {
            config.SaslMechanism = mechanism;
            config.SaslUsername = options.SaslUsername;
            config.SaslPassword = options.SaslPassword;
        }

        return config;
    }

    public static ProducerConfig CreateProducerConfig(KafkaOptions options) =>
        new(CreateClientConfig(options))
        {
            // Exactly-once-ish producer semantics: no duplicates or reordering on internal retry.
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageSendMaxRetries = 10,
            RetryBackoffMs = 200,
            LingerMs = 5,
            CompressionType = CompressionType.Snappy,
            MessageTimeoutMs = 30000,
        };

    public static ConsumerConfig CreateConsumerConfig(KafkaOptions options, string groupId) =>
        new(CreateClientConfig(options))
        {
            GroupId = groupId,
            // Offsets are committed by the worker only after a message is handled, so a crash replays it.
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = options.SessionTimeoutMs,
            MaxPollIntervalMs = Math.Max(options.SessionTimeoutMs * 2, 300000),
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky,
        };

    public static AdminClientConfig CreateAdminConfig(KafkaOptions options) =>
        new(CreateClientConfig(options));
}
