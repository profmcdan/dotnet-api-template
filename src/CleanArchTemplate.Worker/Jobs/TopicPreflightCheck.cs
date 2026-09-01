using CleanArchTemplate.Infrastructure.Messaging;
using CleanArchTemplate.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanArchTemplate.Worker.Jobs;

/// <summary>
/// Warns at startup about topics the migrator has not created.
/// <para>
/// A consumer subscribed to a non-existent topic simply sits idle, which looks identical to
/// "no traffic yet" - this turns that silent failure into a log line on the first minute.
/// </para>
/// </summary>
internal sealed class TopicPreflightCheck(
    IKafkaTopicProvisioner provisioner,
    IOptions<KafkaOptions> options,
    ILogger<TopicPreflightCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.ValidateTopicsOnStart)
        {
            return;
        }

        try
        {
            var missing = await provisioner.ListMissingTopicsAsync(cancellationToken);

            if (missing.Count > 0)
            {
                WorkerLog.MissingTopics(logger, string.Join(", ", missing));
            }
        }
        catch (Exception ex)
        {
            // A broker that is not up yet is not a reason to refuse to start; consumers will retry.
            WorkerLog.CleanupFailed(logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
