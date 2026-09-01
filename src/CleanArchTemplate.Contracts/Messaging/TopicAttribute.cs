namespace CleanArchTemplate.Contracts.Messaging;

/// <summary>
/// Binds an event type to its logical topic. The name here is the suffix; the configured
/// topic prefix is applied at runtime by <c>ITopicResolver</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TopicAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
