namespace CleanArchTemplate.Application.Abstractions.Time;

/// <summary>Injected rather than calling <c>DateTimeOffset.UtcNow</c>, so time is testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
