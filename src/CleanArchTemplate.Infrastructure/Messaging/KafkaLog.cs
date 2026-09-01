using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Infrastructure.Messaging;

internal static partial class KafkaLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Produced to {Topic} partition {Partition} offset {Offset}")]
    public static partial void Produced(ILogger logger, string topic, int partition, long offset);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Kafka producer error: {Reason} (fatal: {IsFatal})")]
    public static partial void ProducerError(ILogger logger, string reason, bool isFatal);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Kafka consumer error on {Topic}: {Reason} (fatal: {IsFatal})")]
    public static partial void ConsumerError(ILogger logger, string topic, string reason, bool isFatal);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Created topic {Topic} with {Partitions} partitions")]
    public static partial void TopicCreated(ILogger logger, string topic, int partitions);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Debug, Message = "Topic {Topic} already exists")]
    public static partial void TopicExists(ILogger logger, string topic);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Message on {Topic} offset {Offset} failed attempt {Attempt}; retrying")]
    public static partial void MessageRetry(ILogger logger, string topic, long offset, int attempt);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Error, Message = "Message on {Topic} offset {Offset} exhausted retries; sent to dead letter")]
    public static partial void MessageDeadLettered(ILogger logger, Exception exception, string topic, long offset);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Consumer {ConsumerName} subscribed to {Topic} in group {GroupId}")]
    public static partial void ConsumerStarted(ILogger logger, string consumerName, string topic, string groupId);
}
