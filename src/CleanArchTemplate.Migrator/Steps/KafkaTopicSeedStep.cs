using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Migrator.Steps;

/// <summary>
/// Creates every topic in the catalogue under the configured prefix. Explicit creation is what
/// gives each topic its intended partition count and retention - broker auto-creation would hand
/// out one partition and the broker default.
/// </summary>
internal sealed class KafkaTopicSeedStep(
    IKafkaTopicProvisioner provisioner,
    ITopicResolver topics,
    ILogger<KafkaTopicSeedStep> logger) : IMigrationStep
{
    public string Name => "kafka-topics";

    public int Order => 20;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        MigratorLog.SeedingTopics(logger, string.IsNullOrEmpty(topics.Prefix) ? "(none)" : topics.Prefix);

        var created = await provisioner.EnsureTopicsAsync(cancellationToken);

        if (created.Count == 0)
        {
            MigratorLog.TopicsUpToDate(logger);
            return;
        }

        MigratorLog.TopicsCreated(logger, created.Count, string.Join(", ", created));
    }
}
