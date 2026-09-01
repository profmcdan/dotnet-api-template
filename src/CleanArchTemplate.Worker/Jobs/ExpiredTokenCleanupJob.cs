using CleanArchTemplate.Application.Abstractions.Persistence;
using CleanArchTemplate.Application.Abstractions.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanArchTemplate.Worker.Jobs;

/// <summary>
/// Deletes refresh tokens that expired long enough ago to be useless.
/// <para>
/// The grace period is intentional: a just-expired token is still evidence for reuse detection,
/// so keeping it a while longer means a stolen token is reported rather than silently unknown.
/// </para>
/// </summary>
internal sealed class ExpiredTokenCleanupJob(IServiceScopeFactory scopeFactory, ILogger<ExpiredTokenCleanupJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var removed = await tokens.DeleteExpiredAsync(clock.UtcNow - GracePeriod, stoppingToken);

                if (removed > 0)
                {
                    WorkerLog.TokensPurged(logger, removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                WorkerLog.CleanupFailed(logger, ex);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
