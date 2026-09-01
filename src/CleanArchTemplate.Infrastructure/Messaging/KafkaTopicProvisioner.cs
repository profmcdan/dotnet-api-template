using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using BrokerTopicSpec = Confluent.Kafka.Admin.TopicSpecification;
using TopicSpec = CleanArchTemplate.Contracts.Messaging.TopicSpecification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Messaging;

public interface IKafkaTopicProvisioner
{
    /// <summary>Creates any missing topic from the catalogue. Idempotent - safe to run on every deploy.</summary>
    Task<IReadOnlyList<string>> EnsureTopicsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListMissingTopicsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Seeds the topic catalogue. Relying on broker auto-creation gives you one partition and the
/// broker default retention, which is almost never what a topic actually needs - so the migrator
/// creates them explicitly instead.
/// </summary>
internal sealed class KafkaTopicProvisioner(
    IOptions<KafkaOptions> options,
    ITopicResolver topics,
    ILogger<KafkaTopicProvisioner> logger) : IKafkaTopicProvisioner
{
    private readonly KafkaOptions _options = options.Value;

    public async Task<IReadOnlyList<string>> EnsureTopicsAsync(CancellationToken cancellationToken = default)
    {
        using var admin = new AdminClientBuilder(KafkaClientFactory.CreateAdminConfig(_options)).Build();

        var existing = GetExistingTopics(admin);
        var created = new List<string>();
        var toCreate = new List<TopicSpec>();

        foreach (var spec in Topics.All)
        {
            var name = topics.Resolve(spec.Name);

            if (existing.Contains(name))
            {
                KafkaLog.TopicExists(logger, name);
                continue;
            }

            toCreate.Add(spec with { Name = name });
        }

        if (toCreate.Count == 0)
        {
            return created;
        }

        var requests = toCreate.Select(spec => new BrokerTopicSpec
        {
            Name = spec.Name,
            NumPartitions = spec.Partitions > 0 ? spec.Partitions : _options.DefaultPartitions,
            ReplicationFactor = spec.ReplicationFactor > 0 ? spec.ReplicationFactor : _options.DefaultReplicationFactor,
            Configs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retention.ms"] = spec.RetentionMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["cleanup.policy"] = spec.CleanupPolicy,
            },
        }).ToList();

        try
        {
            await admin.CreateTopicsAsync(requests, new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(30) });

            foreach (var spec in toCreate)
            {
                KafkaLog.TopicCreated(logger, spec.Name, spec.Partitions);
                created.Add(spec.Name);
            }
        }
        catch (CreateTopicsException ex)
        {
            // A concurrent migrator may have won the race; only a genuine failure should propagate.
            foreach (var result in ex.Results)
            {
                if (result.Error.Code == ErrorCode.TopicAlreadyExists)
                {
                    KafkaLog.TopicExists(logger, result.Topic);
                    continue;
                }

                if (result.Error.IsError)
                {
                    throw;
                }

                created.Add(result.Topic);
            }
        }

        return created;
    }

    public Task<IReadOnlyList<string>> ListMissingTopicsAsync(CancellationToken cancellationToken = default)
    {
        using var admin = new AdminClientBuilder(KafkaClientFactory.CreateAdminConfig(_options)).Build();
        var existing = GetExistingTopics(admin);

        IReadOnlyList<string> missing = Topics.All
            .Select(spec => topics.Resolve(spec.Name))
            .Where(name => !existing.Contains(name))
            .ToList();

        return Task.FromResult(missing);
    }

    private static HashSet<string> GetExistingTopics(IAdminClient admin) =>
        admin.GetMetadata(TimeSpan.FromSeconds(30))
            .Topics
            .Select(topic => topic.Topic)
            .ToHashSet(StringComparer.Ordinal);
}
