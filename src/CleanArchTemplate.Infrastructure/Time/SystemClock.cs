using CleanArchTemplate.Application.Abstractions.Time;

namespace CleanArchTemplate.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
