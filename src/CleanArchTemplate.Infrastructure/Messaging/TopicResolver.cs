using System.Collections.Concurrent;
using System.Reflection;
using CleanArchTemplate.Application.Abstractions.Messaging;
using CleanArchTemplate.Contracts.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Infrastructure.Messaging;

/// <summary>
/// Applies the deployment topic prefix. Every producer and consumer goes through this, so a
/// mismatched prefix fails uniformly rather than leaving one side silently on the wrong topic.
/// </summary>
internal sealed class TopicResolver : ITopicResolver
{
    private static readonly ConcurrentDictionary<Type, string> LogicalNames = new();
    private readonly string _prefix;

    public TopicResolver(IOptions<KafkaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _prefix = options.Value.TopicPrefix?.Trim().Trim('.') ?? string.Empty;
    }

    public string Prefix => _prefix;

    public string Resolve(string logicalTopic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalTopic);
        return string.IsNullOrEmpty(_prefix) ? logicalTopic : $"{_prefix}.{logicalTopic}";
    }

    public string ResolveFor<TEvent>() where TEvent : IIntegrationEvent => ResolveFor(typeof(TEvent));

    public string ResolveFor(Type eventType)
    {
        var logical = LogicalNames.GetOrAdd(eventType, static type =>
            type.GetCustomAttribute<TopicAttribute>()?.Name
            ?? throw new InvalidOperationException(
                $"'{type.FullName}' has no [Topic] attribute, so it cannot be routed. " +
                "Add one from CleanArchTemplate.Contracts.Messaging.Topics."));

        return Resolve(logical);
    }
}
