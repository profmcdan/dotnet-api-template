using CleanArchTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchTemplate.Api.IntegrationTests.Fixtures;

/// <summary>
/// Recovers the raw invitation token from the outbox payload - the same place the email worker
/// reads it. It deliberately exists nowhere else, so tests have to go through the outbox too.
/// </summary>
internal static class InvitationTokenReader
{
    private const string Marker = "accept-invitation?token=";

    public static async Task<string> ReadAsync(ApiFactory factory, Guid userId, CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(factory, userId, cancellationToken);

        var start = payload.IndexOf(Marker, StringComparison.Ordinal) + Marker.Length;
        var end = payload.IndexOf('"', start);
        return payload[start..end];
    }

    /// <summary>
    /// The payload column is jsonb, which has no LIKE operator, so the recent rows are filtered
    /// in memory rather than pushed into SQL with a cast.
    /// </summary>
    public static async Task<string> ReadPayloadAsync(ApiFactory factory, Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recent = await context.OutboxMessages.AsNoTracking()
            .OrderByDescending(m => m.OccurredAt)
            .Select(m => new { m.Topic, m.Payload })
            .Take(200)
            .ToListAsync(cancellationToken);

        var match = recent.Find(m => m.Payload.Contains(userId.ToString(), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No outbox message was written for user {userId}.");

        return match.Payload;
    }

    public static async Task<(string Topic, string Payload)> ReadMessageAsync(ApiFactory factory, Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recent = await context.OutboxMessages.AsNoTracking()
            .OrderByDescending(m => m.OccurredAt)
            .Select(m => new { m.Topic, m.Payload })
            .Take(200)
            .ToListAsync(cancellationToken);

        var match = recent.Find(m => m.Payload.Contains(userId.ToString(), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No outbox message was written for user {userId}.");

        return (match.Topic, match.Payload);
    }
}
