using CleanArchTemplate.Contracts.Messaging;

namespace CleanArchTemplate.Application.Abstractions.Messaging;

/// <summary>Applies the configured topic prefix to a logical topic name.</summary>
public interface ITopicResolver
{
    string Prefix { get; }

    string Resolve(string logicalTopic);

    string ResolveFor<TEvent>() where TEvent : IIntegrationEvent;

    string ResolveFor(Type eventType);
}
